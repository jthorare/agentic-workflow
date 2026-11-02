using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>
/// The real mint: a JWT signed with the agent GitHub App's key, exchanged for an installation token narrowed to the launch's repository (the C# form of scripts/agent-token.sh).
/// The key is the PEM at AGENT_APP_KEY_FILE, else the macOS keychain item; the JWT and the token are never logged, and GitHub's own message is not surfaced.
/// </summary>
internal sealed partial class GitHubAppTokenSource(Func<string, string?> environment, HttpClient http, TimeProvider clock) : IAgentTokenSource
{
    private const string DefaultApi = "https://api.github.com";
    private const string DefaultAppId = "5259848";
    private const string DefaultInstallationId = "169843852";
    private const string KeychainService = "agentic-workflow-agent-app";
    private static readonly TimeSpan _requestBudget = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _keychainBudget = TimeSpan.FromSeconds(10);
    private static readonly System.Text.Json.JsonSerializerOptions _json = AwJson.CreateOptions(AgentAppJsonContext.Default);

    /// <summary>Builds the client production uses: no redirect followed, no proxy, the request's own 30 s deadline.</summary>
    /// <returns>The client.</returns>
#pragma warning disable CA2000 // the client owns the handler and disposes it
    internal static HttpClient CreateClient() => new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
#pragma warning restore CA2000

    /// <summary>Checks an AGENT_APP_API_URL: GitHub itself, or a loopback http URL with a port (tests); whatever host it names receives the App's JWT.</summary>
    /// <param name="url">The value.</param>
    /// <returns>Whether it may be used.</returns>
    internal static bool IsAllowedApiUrl(string? url) => url is not null && AllowedApi.IsMatch(url);

    /// <inheritdoc/>
    public async ValueTask<Result<AgentToken>> Mint(LaunchId launch, RepoName repo, CancellationToken cancellationToken)
    {
        var api = environment("AGENT_APP_API_URL") is { Length: > 0 } url ? url : DefaultApi;
        if (!IsAllowedApiUrl(api))
        {
            return Refuse(SandboxReasons.NoNetwork, "AGENT_APP_API_URL must be https://api.github.com, or http://127.0.0.1:<port> or http://localhost:<port> for tests");
        }

        var appId = environment("AGENT_APP_ID") is { Length: > 0 } app ? app : DefaultAppId;
        var installation = environment("AGENT_APP_INSTALLATION_ID") is { Length: > 0 } inst ? inst : DefaultInstallationId;
        if (!Digits.IsMatch(appId) || !Digits.IsMatch(installation))
        {
            return Refuse(SandboxReasons.NoKey, "AGENT_APP_ID and AGENT_APP_INSTALLATION_ID must be numbers");
        }

        var pem = await ReadKey(cancellationToken).ConfigureAwait(false);
        if (pem is null)
        {
            return Refuse(SandboxReasons.NoKey, "no AGENT_APP_KEY_FILE and no keychain item");
        }

        var jwt = Sign(pem, appId);
        if (jwt is null)
        {
            return Refuse(SandboxReasons.NoKey, "the key is not a PEM RSA private key");
        }

        return await Exchange(api, installation, jwt, repo, cancellationToken).ConfigureAwait(false);
    }

    private static Error Refuse(string reason, string detail) => SandboxRefusal.For(SindriErrors.LaunchGitHubTokenFailed, reason, detail);

    private async ValueTask<Result<AgentToken>> Exchange(string api, string installation, string jwt, RepoName repo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"{api}/app/installations/{installation}/access_tokens"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        request.Content = new ByteArrayContent(AwJson.Serialize(new InstallationTokenRequestWire([repo.Value]), _json));
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_requestBudget);
        try
        {
            using var response = await http.SendAsync(request, deadline.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsByteArrayAsync(deadline.Token).ConfigureAwait(false);
            return Read(status, body);
        }
        catch (HttpRequestException)
        {
            return Refuse(SandboxReasons.NoNetwork, "GitHub could not be reached");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Refuse(SandboxReasons.NoNetwork, "GitHub did not answer within 30 s");
        }
    }

    private Result<AgentToken> Read(int status, byte[] body)
    {
        var reason = string.Create(CultureInfo.InvariantCulture, $"http-{status:D3}");
        if (status != 201)
        {
            return Refuse(reason, "GitHub refused the request");
        }

        var wire = AwJson.Deserialize<InstallationTokenWire>(body, _json);
        return wire.IsSuccess && wire.Value.Token is { Length: > 0 } token
            ? new AgentToken(token, wire.Value.ExpiresAt ?? clock.GetUtcNow().AddHours(1))
            : Refuse(reason, "GitHub's answer had no token");
    }

    // The RS256 JWT (header.claims.signature, base64url); null when the key can't sign.
    private string? Sign(string pem, string appId)
    {
        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var claims = Base64Url(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $$"""{"iat":{{now - 60}},"exp":{{now + 540}},"iss":"{{appId}}"}""")));
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var signature = rsa.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{header}.{claims}.{Base64Url(signature)}";
        }
        catch (Exception e) when (e is ArgumentException or CryptographicException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async ValueTask<string?> ReadKey(CancellationToken cancellationToken)
    {
        if (environment("AGENT_APP_KEY_FILE") is { Length: > 0 } file)
        {
            try
            {
                return await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        return OperatingSystem.IsMacOS() ? await Keychain(cancellationToken).ConfigureAwait(false) : null;
    }

    // A multi-line secret comes back from 'security -w' hex-encoded; a single-line one as is.
    private static async ValueTask<string?> Keychain(CancellationToken cancellationToken)
    {
        var start = ChildEnvironment.Start("security", ["find-generic-password", "-s", KeychainService, "-a", "private-key", "-w"]);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        Process? security;
        try
        {
            security = Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }

        using (security)
        {
            var text = security is null ? null : await Output(security, cancellationToken).ConfigureAwait(false);
            return text is null ? null : HexOnly.IsMatch(text) ? Encoding.UTF8.GetString(Convert.FromHexString(text)) : text;
        }
    }

    private static async Task<string?> Output(Process security, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_keychainBudget);
        try
        {
            var output = security.StandardOutput.ReadToEndAsync(deadline.Token);
            _ = security.StandardError.ReadToEndAsync(deadline.Token);
            await security.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var text = (await output.ConfigureAwait(false)).Trim();
            return security.ExitCode != 0 || text.Length == 0 ? null : text;
        }
        catch (OperationCanceledException)
        {
            security.Kill();
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return null;
        }
    }

    [GeneratedRegex("^(https://api\\.github\\.com|http://(127\\.0\\.0\\.1|localhost):[0-9]{1,5})\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex AllowedApi { get; }

    [GeneratedRegex("^[0-9]+\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex Digits { get; }

    [GeneratedRegex("^([0-9A-Fa-f]{2})+\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex HexOnly { get; }
}
