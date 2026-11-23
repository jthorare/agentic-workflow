using System.Net;
using System.Threading.Channels;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections;
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
