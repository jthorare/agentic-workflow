using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Infrastructure.Sandbox.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Sandbox.Egress;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Sandbox.Launch;

/// <summary>The agent GitHub App mint against a loopback fake of GitHub's token route: the JWT it signs, the request it makes, and the three -018 reasons.</summary>
[Lane(TestLane.Integration)]
public sealed class GitHubAppTokenSourceTests : IDisposable
{
    private static readonly RepoName _repo = RepoName.From("web");
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly TempDirectory _temp = new();
    private readonly RSA _key = RSA.Create(2048);
    private readonly Dictionary<string, string?> _env = [];
    private readonly FakeGitHubApi _api = new();

    public GitHubAppTokenSourceTests()
    {
        var keyFile = Path.Combine(_temp.Path, "app.pem");
        File.WriteAllText(keyFile, _key.ExportPkcs8PrivateKeyPem());
        _env["AGENT_APP_KEY_FILE"] = keyFile;
        _env["AGENT_APP_API_URL"] = _api.Url;
    }

    public void Dispose()
    {
        _api.Dispose();
        _key.Dispose();
        _temp.Dispose();
    }

    private GitHubAppTokenSource Source() => new(name => _env.GetValueOrDefault(name), GitHubAppTokenSource.CreateClient(), _clock);

    private static LaunchId Launch() => A.LaunchRequest().Build().Launch;

    private static string Reason(Kernel.Error error) => error.Extensions!.Single(e => e.Key == "reason").Value.GetString()!;

    [Fact]
    public async Task A_201_gives_the_token_and_its_expiry_and_the_request_is_the_installation_token_call()
    {
        _api.Reply(201, """{"token":"ghs_abc","expires_at":"2026-10-28T12:00:00Z"}""");

        var minted = await Source().Mint(Launch(), _repo, CancellationToken.None);

        Expect.True(minted.IsSuccess);
        Expect.Equal("ghs_abc", minted.Value.Value);
        Expect.Equal(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), minted.Value.ExpiresAt);
        var request = _api.Requests.Single();
        Expect.Equal("POST /app/installations/169843852/access_tokens", request.Line);
        Expect.Equal("""{"repositories":["web"]}""", request.Body);
        Expect.Equal("application/vnd.github+json", request.Headers["Accept"]);
        Expect.Equal("2022-11-28", request.Headers["X-GitHub-Api-Version"]);
    }

    [Fact]
    public async Task The_token_never_shows_in_the_printed_form_of_the_result()
    {
        _api.Reply(201, """{"token":"ghs_secret","expires_at":"2026-10-28T12:00:00Z"}""");

        var minted = await Source().Mint(Launch(), _repo, CancellationToken.None);

        Expect.True(!minted.Value.ToString().Contains("ghs_secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_JWT_is_RS256_signed_with_the_key_and_carries_the_app_id_and_a_ten_minute_window()
    {
        _api.Reply(201, """{"token":"ghs_abc","expires_at":"2026-10-28T12:00:00Z"}""");
        _env["AGENT_APP_ID"] = "4242";
        _env["AGENT_APP_INSTALLATION_ID"] = "777";

        _ = await Source().Mint(Launch(), _repo, CancellationToken.None);

        var request = _api.Requests.Single();
        Expect.Equal("POST /app/installations/777/access_tokens", request.Line);
        var parts = request.Headers["Authorization"]["Bearer ".Length..].Split('.');
        Expect.Equal(3, parts.Length);
        Expect.Equal("""{"alg":"RS256","typ":"JWT"}""", Encoding.UTF8.GetString(Base64Url(parts[0])));
        using var claims = JsonDocument.Parse(Base64Url(parts[1]));
        var now = KnownInstants.Epoch.ToUnixTimeSeconds();
        Expect.Equal(now - 60, claims.RootElement.GetProperty("iat").GetInt64());
        Expect.Equal(now + 540, claims.RootElement.GetProperty("exp").GetInt64());
        Expect.Equal("4242", claims.RootElement.GetProperty("iss").GetString());
        Expect.True(_key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64Url(parts[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public async Task A_401_refuses_with_http_401()
    {
        _api.Reply(401, """{"message":"Bad credentials"}""");

        var minted = await Source().Mint(Launch(), _repo, CancellationToken.None);

        Expect.True(minted.IsError && string.Equals(minted.Error.Code, SindriErrors.LaunchGitHubTokenFailed.Code, StringComparison.Ordinal));
        Expect.Equal("http-401", Reason(minted.Error));
    }

    [Fact]
    public async Task A_closed_port_refuses_with_no_network()
    {
        _env["AGENT_APP_API_URL"] = FakeGitHubApi.ClosedUrl();

        var minted = await Source().Mint(Launch(), _repo, CancellationToken.None);

        Expect.True(minted.IsError);
        Expect.Equal(SandboxReasons.NoNetwork, Reason(minted.Error));
    }

    [Fact]
    public async Task No_key_file_and_no_keychain_refuses_with_no_key_and_asks_nothing_of_GitHub()
    {
        _env["AGENT_APP_KEY_FILE"] = Path.Combine(_temp.Path, "missing.pem");

        var minted = await Source().Mint(Launch(), _repo, CancellationToken.None);

        Expect.True(minted.IsError);
        Expect.Equal(SandboxReasons.NoKey, Reason(minted.Error));
        Expect.Equal(0, _api.Requests.Count);
    }

    [Theory]
    [InlineData("https://api.github.com", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("http://localhost:9", true)]
    [InlineData("http://api.github.com", false)]
    [InlineData("https://api.github.com/", false)]
    [InlineData("https://evil.example", false)]
    [InlineData("http://127.0.0.1", false)]
    [InlineData("http://127.0.0.1:80/x", false)]
    [InlineData("http://localhost.evil.example:80", false)]
    public void The_API_url_allowlist(string url, bool allowed) =>
        Expect.Equal(allowed, GitHubAppTokenSource.IsAllowedApiUrl(url));

    [Fact]
    public async Task A_url_off_the_allowlist_refuses_before_the_key_or_the_network_is_touched()
    {
        _env["AGENT_APP_API_URL"] = "https://evil.example";

        var minted = await Source().Mint(Launch(), _repo, CancellationToken.None);

        Expect.True(minted.IsError && string.Equals(minted.Error.Code, SindriErrors.LaunchGitHubTokenFailed.Code, StringComparison.Ordinal));
        Expect.Equal(0, _api.Requests.Count);
    }

    private static byte[] Base64Url(string text) =>
        Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/').PadRight((text.Length + 3) / 4 * 4, '='));
}
