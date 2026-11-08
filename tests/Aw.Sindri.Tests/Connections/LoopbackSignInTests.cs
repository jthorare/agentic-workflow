using System.Security.Cryptography;
using System.Web;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Infrastructure.Connections;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The real loopback sign-in and token client against the fake provider, through AddConnection, with the fake keychain and no real browser.</summary>
[Lane(TestLane.Integration)]
public sealed class LoopbackSignInTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AddConnectionRequest Request() => new("fake:work", "fake", Kind: null, Client: "client-123", KeyEnv: null, Scopes: [], Wait: true);

    [Fact(Timeout = 60_000)]
    public async Task The_callback_stores_tokens_in_the_keychain_and_closes_the_listener()
    {
        var ct = TestContext.Current.CancellationToken;
        using var world = new SignInWorld(TimeSpan.FromSeconds(30));

        var added = await world.Service.Handle(Request(), ct);

        Assert.True(added.IsSuccess, added.IsError ? added.Error.Message : null);
        Assert.Equal("connected", added.Value.Connection.Status);
        var row = Assert.Single(world.Harness.Store.Rows);
        var stored = world.Harness.Secrets.Tokens(row.SecretPointer!.ToString())!;
        Assert.Equal(world.Server.IssuedAccessTokens[0], stored.AccessToken);
        Assert.Equal(world.Server.IssuedRefreshTokens[0], stored.RefreshToken);
        _ = Assert.NotNull(stored.ExpiresAt);
        var tokenCall = Assert.Single(world.Server.Requests, static r => r.Path == "/token");
        Assert.Equal(43, tokenCall.Form["code_verifier"].Length);   // 32 random bytes as base64url
        await world.AssertListenerClosed();
    }

    [Fact(Timeout = 60_000)]
    public async Task A_denied_consent_or_a_timeout_leaves_no_connection_row()
    {
        var ct = TestContext.Current.CancellationToken;
        using var denied = new SignInWorld(TimeSpan.FromSeconds(30));
        denied.Server.DenyConsent = true;

        var deniedAdd = await denied.Service.Handle(Request(), ct);

        Assert.Equal(SindriErrors.ConnectionConsentDenied.Code, deniedAdd.Error.Code);
        Assert.Empty(denied.Harness.Store.Rows);
        Assert.Empty(denied.Harness.Secrets.Pointers);
        Assert.DoesNotContain(denied.Server.Requests, static r => r.Path == "/token");
        await denied.AssertListenerClosed();

        using var silent = new SignInWorld(TimeSpan.FromMilliseconds(300));
        silent.Browser.Unavailable = true;   // nobody opens the page

        var timedOut = await silent.Service.Handle(Request(), ct);

        Assert.Equal(SindriErrors.ConnectionSignInTimeout.Code, timedOut.Error.Code);
        Assert.Empty(silent.Harness.Store.Rows);
        Assert.Empty(silent.Harness.Secrets.Pointers);
        await silent.AssertListenerClosed();
    }

    [Fact(Timeout = 60_000)]
    public async Task A_state_mismatch_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var world = new SignInWorld(TimeSpan.FromSeconds(30));
        world.Server.ReturnedState = "forged-state";

        var added = await world.Service.Handle(Request(), ct);

        Assert.Equal(SindriErrors.ConnectionStateMismatch.Code, added.Error.Code);
        Assert.DoesNotContain(world.Server.Requests, static r => r.Path == "/token");   // the code is never traded
        Assert.Empty(world.Harness.Store.Rows);
        Assert.Empty(world.Harness.Secrets.Pointers);
        await world.AssertListenerClosed();
    }

    [Fact(Timeout = 60_000)]
    public async Task The_consent_url_has_the_loopback_redirect_the_preset_scopes_and_a_challenge_never_the_verifier()
    {
        var ct = TestContext.Current.CancellationToken;
        using var world = new SignInWorld(TimeSpan.FromSeconds(30));

        _ = await world.Service.Handle(Request(), ct);

        var opened = Assert.Single(world.Browser.Opened);
        var query = HttpUtility.ParseQueryString(opened.Query);
        Assert.StartsWith("http://127.0.0.1:", query["redirect_uri"], StringComparison.Ordinal);
        Assert.Equal("read write", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        var verifier = world.Server.Requests.Single(static r => r.Path == "/token").Form["code_verifier"];
        Assert.DoesNotContain(verifier, opened.ToString(), StringComparison.Ordinal);
    }

    private sealed class SignInWorld : IDisposable
    {
        private readonly HttpClient _http = new();

        public SignInWorld(TimeSpan timeout)
        {
            Server = new FakeOAuthServer();
            Harness = new ConnectionHarness { Presets = new FakeConnectionPresets(Server.Preset("fake")) };
            Browser = new FakeBrowser();
            var signIn = new LoopbackSignIn(new HttpOAuthClient(_http, Harness.Clock), Browser, timeout, RandomNumberGenerator.GetBytes);
            Service = Harness.Add(signIn);
        }

        public FakeOAuthServer Server { get; }

        public ConnectionHarness Harness { get; }

        internal FakeBrowser Browser { get; }

        public AddConnectionService Service { get; }

        public async Task AssertListenerClosed()
        {
            var redirect = HttpUtility.ParseQueryString(Browser.Opened[0].Query)["redirect_uri"]!;
            _ = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                using var response = await _http.GetAsync(new Uri(redirect), Token);
            });
        }

        public void Dispose()
        {
            _http.Dispose();
            Browser.Dispose();
            Server.Dispose();
        }
    }
}
