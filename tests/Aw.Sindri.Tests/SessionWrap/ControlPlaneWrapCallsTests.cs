using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Features.GetRunSuspension;
using Aw.Sindri.Features.RecordLimitHit;
using Aw.Sindri.Features.SessionExit;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The wrap's calls over the CLI client: the suspension question and the exit with a limit, each proven by the launch secret.</summary>
[Lane(TestLane.Unit)]
public sealed class ControlPlaneWrapCallsTests
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly LaunchSecret _proof = new("a-proof");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Route => $"/sindri/launches/{A.FirstLaunchId.Value}/suspended";

    private static ControlPlaneWrapCalls Calls(HttpMessageHandler handler)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: handler);
        return new ControlPlaneWrapCalls(client);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Suspended_reads_the_answer(bool suspended)
    {
        var api = new RoutedApi().On(Route, HttpStatusCode.OK, new RunSuspensionView(suspended));

        var answer = await Calls(api).Suspended(A.FirstLaunchId, _proof, Token);

        Assert.True(answer.IsSuccess, answer.IsError ? answer.Error.ToString() : null);
        var call = Assert.Single(api.Calls, static c => string.Equals(c.Path, "/sindri/launches/" + A.FirstLaunchId.Value + "/suspended", StringComparison.Ordinal));
        Expect.Equal((suspended, "a-proof"), (answer.Value, call.Headers[SindriHeaders.LaunchSecret]));
    }

    [Fact]
    public async Task Suspended_when_unreachable_is_an_error()
    {
        var answer = await Calls(new Unreachable()).Suspended(A.FirstLaunchId, _proof, Token);

        Expect.True(answer.IsError);
    }

    [Fact]
    public async Task Exit_with_a_limit_sends_it_in_the_body()
    {
        var api = new RoutedApi().OnPost($"/sindri/sessions/{A.FirstLaunchId.Value}/exit", HttpStatusCode.OK, new SessionExitResponse(A.FirstLaunchId));

        var sent = await Calls(api).Exit(A.FirstLaunchId, _proof, new WrapExit(137, Limit: new LimitEnd(LimitKind.Wall, Limit: 3600, Peak: 3601)), Token);

        Assert.True(sent.IsSuccess, sent.IsError ? sent.Error.ToString() : null);
        var body = Assert.Single(api.Calls, static c => string.Equals(c.Method, "POST", StringComparison.Ordinal)).Body!;
        Expect.True(body.Contains("\"kind\":\"wall\"", StringComparison.Ordinal) && body.Contains("\"limit\":3600", StringComparison.Ordinal) && body.Contains("\"peak\":3601", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LimitHit_posts_the_kind_and_peak_with_the_launch_secret()
    {
        var api = new RoutedApi().OnPost($"/sindri/launches/{A.FirstLaunchId.Value}/limit-hit", HttpStatusCode.OK, new RecordLimitHitResponse(A.FirstLaunchId));

        var sent = await Calls(api).LimitHit(A.FirstLaunchId, _proof, LimitHitKind.Pids, 2048, Token);

        Assert.True(sent.IsSuccess, sent.IsError ? sent.Error.ToString() : null);
        var call = Assert.Single(api.Calls, static c => string.Equals(c.Method, "POST", StringComparison.Ordinal));
        Expect.True(call.Body!.Contains("\"kind\":\"pids\"", StringComparison.Ordinal) && call.Body.Contains("\"peak\":2048", StringComparison.Ordinal));
        Expect.Equal("a-proof", call.Headers[SindriHeaders.LaunchSecret]);
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}
