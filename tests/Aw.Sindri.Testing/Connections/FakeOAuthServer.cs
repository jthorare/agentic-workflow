using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Testing.Connections;

/// <summary>
/// A provider on 127.0.0.1 for the sign-in tests (connections PR 10a): /authorize (auto-approves, denies, or returns a wrong state), /token
/// (authorization_code with the PKCE verifier checked, refresh_token that can revoke or rotate) and /revoke. It is test infrastructure and works:
/// the red tests need it to run. It is an HttpListener, as the other loopback fakes are, so Testing needs no web framework.
/// </summary>
public sealed class FakeOAuthServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _challengeByCode = new(StringComparer.Ordinal);
    private readonly List<OAuthServerRequest> _requests = [];
    private int _codes;
    private int _tokens;

    /// <summary>Starts the server on a free loopback port.</summary>
    public FakeOAuthServer()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        Url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
        _listener.Prefixes.Add(Url + "/");
        _listener.Start();
        _ = Serve();
    }

    /// <summary>Gets the server's base URL.</summary>
    public string Url { get; }

    /// <summary>Gets or sets a value indicating whether /authorize answers with error=access_denied (the owner pressed Deny).</summary>
    public bool DenyConsent { get; set; }

    /// <summary>Gets or sets the state /authorize hands back instead of the one it was given (a forged or stale callback); null returns the right one.</summary>
    public string? ReturnedState { get; set; }

    /// <summary>Gets or sets a value indicating whether /token refuses every refresh with invalid_grant (the provider revoked the grant).</summary>
    public bool RevokeRefresh { get; set; }

    /// <summary>Gets or sets a value indicating whether a refresh hands out a new refresh token.</summary>
    public bool RotateRefresh { get; set; }

    /// <summary>Gets or sets the access token lifetime in seconds.</summary>
    public int ExpiresInSeconds { get; set; } = 3600;

    /// <summary>Gets the requests received so far.</summary>
    public IReadOnlyList<OAuthServerRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Gets the access tokens handed out, in order.</summary>
    public IList<string> IssuedAccessTokens { get; } = [];

    /// <summary>Gets the refresh tokens handed out, in order.</summary>
    public IList<string> IssuedRefreshTokens { get; } = [];

    /// <summary>Gets the tokens /revoke was asked to revoke.</summary>
    public IList<string> RevokedTokens { get; } = [];

    /// <summary>A preset that points at this server. Plain http is fine here: the preset type does not check it, the shipped-presets test does.</summary>
    /// <param name="key">The preset key.</param>
    /// <param name="revoke">Whether the preset has a revoke endpoint.</param>
    /// <returns>The preset.</returns>
    public ConnectionPreset Preset(string key = "fake", bool revoke = true) => new(
        key,
        "Fake provider",
        ConnectionKind.OAuth,
        AuthorizeUrl: Url + "/authorize",
        TokenUrl: Url + "/token",
        RevokeUrl: revoke ? Url + "/revoke" : null,
        DefaultScopes: ["read", "write"],
        HostGroup: "fake",
        LoopbackAllowed: true,
        SignInNote: "A fake provider.",
        PollableEvents: [],
        ProbeUrl: Url + "/me");

    /// <inheritdoc />
    public void Dispose() => _listener.Close();

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(CancellationToken.None);
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(CancellationToken.None);
            var form = ParseForm(context.Request.Url!.Query.TrimStart('?') + "&" + body);
            lock (_gate)
            {
                _requests.Add(new OAuthServerRequest(context.Request.HttpMethod, context.Request.Url!.AbsolutePath, form));
            }

            switch (context.Request.Url!.AbsolutePath)
            {
                case "/authorize":
                    Authorize(context, form);
                    break;
                case "/token":
                    Token(context, form);
                    break;
                case "/revoke":
                    Revoke(context, form);
                    break;
                default:
                    Reply(context, 404, "{}");
                    break;
            }
        }
    }

    private void Authorize(HttpListenerContext context, Dictionary<string, string> form)
    {
        var redirect = form.GetValueOrDefault("redirect_uri", string.Empty);
        var state = ReturnedState ?? form.GetValueOrDefault("state", string.Empty);
        string location;
        if (DenyConsent)
        {
            location = redirect + "?error=access_denied&state=" + Uri.EscapeDataString(state);
        }
        else
        {
            string code;
            lock (_gate)
            {
                code = "code-" + (++_codes).ToString(CultureInfo.InvariantCulture);
                _challengeByCode[code] = form.GetValueOrDefault("code_challenge", string.Empty);
            }

            location = redirect + "?code=" + code + "&state=" + Uri.EscapeDataString(state);
        }

        context.Response.StatusCode = 302;
        context.Response.RedirectLocation = location;
        context.Response.Close();
    }

    private void Token(HttpListenerContext context, Dictionary<string, string> form)
    {
        var grant = form.GetValueOrDefault("grant_type", string.Empty);
        if (grant == "authorization_code")
        {
            string? challenge;
            lock (_gate)
            {
                _ = _challengeByCode.Remove(form.GetValueOrDefault("code", string.Empty), out challenge);
            }

            var verifier = form.GetValueOrDefault("code_verifier", string.Empty);
            var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            if (challenge is null || challenge != expected)
            {
                Reply(context, 400, """{"error":"invalid_grant"}""");
                return;
            }

            Reply(context, 200, Tokens(NewRefresh()));
            return;
        }

        if (grant == "refresh_token")
        {
            if (RevokeRefresh)
            {
                Reply(context, 400, """{"error":"invalid_grant","error_description":"the grant was revoked"}""");
                return;
            }

            var presented = form.GetValueOrDefault("refresh_token", string.Empty);
            Reply(context, 200, Tokens(RotateRefresh ? NewRefresh() : presented));
            return;
        }

        Reply(context, 400, """{"error":"unsupported_grant_type"}""");
    }

    private void Revoke(HttpListenerContext context, Dictionary<string, string> form)
    {
        lock (_gate)
        {
            RevokedTokens.Add(form.GetValueOrDefault("token", string.Empty));
        }

        Reply(context, 200, "{}");
    }

    private string NewRefresh()
    {
        lock (_gate)
        {
            var refresh = "refresh-" + (++_tokens).ToString(CultureInfo.InvariantCulture) + "-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            IssuedRefreshTokens.Add(refresh);
            return refresh;
        }
    }

    private string Tokens(string refresh)
    {
        string access;
        lock (_gate)
        {
            access = "access-" + (++_tokens).ToString(CultureInfo.InvariantCulture) + "-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(8));
            IssuedAccessTokens.Add(access);
        }

        return JsonSerializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["access_token"] = access,
            ["refresh_token"] = refresh,
            ["token_type"] = "Bearer",
            ["expires_in"] = ExpiresInSeconds,
        });
    }

    private static void Reply(HttpListenerContext context, int status, string json)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.OutputStream.Write(bytes);
        context.Response.Close();
    }

    private static Dictionary<string, string> ParseForm(string text)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var at = pair.IndexOf('=', StringComparison.Ordinal);
            var key = Uri.UnescapeDataString((at < 0 ? pair : pair[..at]).Replace('+', ' '));
            var value = at < 0 ? string.Empty : Uri.UnescapeDataString(pair[(at + 1)..].Replace('+', ' '));
            form[key] = value;
        }

        return form;
    }
}
