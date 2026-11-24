using System.Net;
using System.Threading.Channels;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Infrastructure.Connections;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The GitHub device flow (first-run spec decision 13, gap 3) against a fake device endpoint and a fake clock: never github.com.</summary>
public sealed class GitHubDeviceSignInTests : IDisposable
{
    private const string Client = "Iv1.sindri-test";

    private static readonly ConnectionPreset _github = new("github", "GitHub", ConnectionKind.OAuth, "https://github.com/login/oauth/authorize", "https://github.com/login/oauth/access_token", RevokeUrl: null,
        DefaultScopes: ["repo", "read:org"], HostGroup: "github", LoopbackAllowed: true, SignInNote: string.Empty, PollableEvents: [], ProbeUrl: "https://api.github.com/user")
    {
        DeviceCodeUrl = "https://github.com/login/device/code",
    };

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeGitHub _fake;
    private readonly HttpClient _http;

    public GitHubDeviceSignInTests()
    {
        _fake = new FakeGitHub(_clock);
        _http = new HttpClient(_fake);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _http.Dispose();

    private GitHubDeviceSignIn SignIn() => new(_http, _clock);

    [Fact, Trait("Lane", "unit")]
    public async Task Device_flow_polls_at_githubs_interval_and_returns_the_token_once_authorized()
    {
        _fake.Device = """{"device_code":"dc-secret","user_code":"ABCD-1234","verification_uri":"https://github.com/login/device","expires_in":900,"interval":5}""";
        _fake.Polls.Enqueue("""{"error":"authorization_pending"}""");
        _fake.Polls.Enqueue("""{"access_token":"gho_device","token_type":"bearer","scope":"repo,read:org"}""");

        var started = await SignIn().Start(_github, Client, [], Token);
        Assert.True(started.IsSuccess, started.IsError ? started.Error.ToString() : null);
        Assert.Equal(("ABCD-1234", new Uri("https://github.com/login/device"), KnownInstants.Epoch.AddSeconds(900)), (started.Value.UserCode, started.Value.VerificationUri, started.Value.ExpiresAt));
        Assert.Equal("client_id=Iv1.sindri-test&scope=repo+read%3Aorg", Assert.Single(_fake.Bodies));

        var completing = SignIn().Complete(_github, Client, started.Value, Token).AsTask();
        _clock.Advance(TimeSpan.FromSeconds(5));
        var first = await _fake.NextPoll(Token);
        _clock.Advance(TimeSpan.FromSeconds(5));
        var second = await _fake.NextPoll(Token);
        var tokens = await completing;

        Assert.Equal((KnownInstants.Epoch.AddSeconds(5), KnownInstants.Epoch.AddSeconds(10)), (first, second));
        Assert.True(tokens.IsSuccess, tokens.IsError ? tokens.Error.ToString() : null);
        Assert.Equal("gho_device", tokens.Value.AccessToken);
        Assert.Equal("client_id=Iv1.sindri-test&device_code=dc-secret&grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", _fake.Bodies[^1]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Slow_down_adds_five_seconds_to_the_wait()
    {
        var started = await Started(interval: "5", expiresIn: "900");
        _fake.Polls.Enqueue("""{"error":"slow_down"}""");
        _fake.Polls.Enqueue("""{"access_token":"gho_device"}""");

        var completing = SignIn().Complete(_github, Client, started, Token).AsTask();
        _clock.Advance(TimeSpan.FromSeconds(5));
        _ = await _fake.NextPoll(Token);
        _clock.Advance(TimeSpan.FromSeconds(10));
        var second = await _fake.NextPoll(Token);

        Assert.Equal(KnownInstants.Epoch.AddSeconds(15), second);
        Assert.Equal("gho_device", (await completing).Value.AccessToken);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_code_that_expires_before_the_owner_approves_ends_as_expired_without_a_token()
    {
        var started = await Started(interval: "5", expiresIn: "8");

        var completing = SignIn().Complete(_github, Client, started, Token).AsTask();
        _clock.Advance(TimeSpan.FromSeconds(5));
        _ = await _fake.NextPoll(Token);   // pending; the next wait would pass the code's expiry
        var result = await completing;

        Assert.Equal(SindriErrors.ConnectionSignInTimeout.Code, result.Error.Code);
        Assert.DoesNotContain("dc-secret", result.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("access_denied", "SND-CONN-009")]
    [InlineData("expired_token", "SND-CONN-003")]
    [InlineData("device_flow_disabled", "SND-SETUP-013")]
    [InlineData("something_new", "SND-CONN-012")]
    public async Task A_final_poll_error_ends_the_sign_in_with_its_code(string error, string code)
    {
        var started = await Started(interval: "5", expiresIn: "900");
        _fake.Polls.Enqueue($$"""{"error":"{{error}}","error_description":"dc-secret leaked"}""");

        var completing = SignIn().Complete(_github, Client, started, Token).AsTask();
        _clock.Advance(TimeSpan.FromSeconds(5));
        var result = await completing;

        Assert.Equal(code, result.Error.Code);
        Assert.DoesNotContain("dc-secret", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_app_github_refuses_at_the_device_endpoint_is_setup_013()
    {
        _fake.Device = """{"error":"unauthorized_client","error_description":"blocked by your org"}""";

        var started = await SignIn().Start(_github, Client, [], Token);

        Assert.Equal(SindriErrors.SetupOauthBlocked.Code, started.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_preset_without_a_device_endpoint_has_no_device_sign_in()
    {
        var started = await SignIn().Start(_github with { DeviceCodeUrl = null }, Client, [], Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, started.Error.Code);
        Assert.Empty(_fake.Bodies);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_authorization_never_prints_its_device_code()
    {
        var authorization = new DeviceAuthorization("dc-secret", "ABCD-1234", new Uri("https://github.com/login/device"), KnownInstants.Epoch, TimeSpan.FromSeconds(5));

        Assert.DoesNotContain("dc-secret", authorization.ToString(), StringComparison.Ordinal);
    }

    private async Task<DeviceAuthorization> Started(string interval, string expiresIn)
    {
        _fake.Device = $$"""{"device_code":"dc-secret","user_code":"ABCD-1234","verification_uri":"https://github.com/login/device","expires_in":{{expiresIn}},"interval":{{interval}}}""";
        return (await SignIn().Start(_github, Client, [], Token)).Value;
    }

    /// <summary>GitHub's device and token endpoints: a fixed device answer, a queue of poll answers, and the clock time of each poll.</summary>
    private sealed class FakeGitHub(TimeProvider clock) : HttpMessageHandler
    {
        private readonly Channel<DateTimeOffset> _polled = Channel.CreateUnbounded<DateTimeOffset>();

        public string Device { get; set; } = "{}";

        public Queue<string> Polls { get; } = new();

        public List<string> Bodies { get; } = [];

        public async Task<DateTimeOffset> NextPoll(CancellationToken cancellationToken)
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(TimeSpan.FromSeconds(10));
            return await _polled.Reader.ReadAsync(limit.Token).ConfigureAwait(false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var path = request.RequestUri?.AbsolutePath;
            if (string.Equals(path, "/login/device/code", StringComparison.Ordinal))
            {
                return Json(Device);
            }

            _ = _polled.Writer.TryWrite(clock.GetUtcNow());
            return Json(Polls.Count > 0 ? Polls.Dequeue() : """{"error":"authorization_pending"}""");
        }

        private static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };
    }
}
