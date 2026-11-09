using System.Net;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Commands;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Control;

/// <summary>The CLI's call cap: none by default, AW_SINDRI_CLI_TIMEOUT when set, and a short bound on connecting only.</summary>
public sealed class CliTimeoutTests
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    public void Unset_empty_or_zero_is_no_cap(string? value) =>
        Assert.Null(CliTimeout.Read(name => name == CliTimeout.Variable ? value : null).Value);

    [Fact, Trait("Lane", "unit")]
    public void Seconds_are_the_cap() =>
        Assert.Equal(TimeSpan.FromSeconds(90), CliTimeout.Read(_ => "90").Value);

    [Theory, Trait("Lane", "unit")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1.5")]
    public void An_invalid_value_is_a_usage_error(string value)
    {
        var read = CliTimeout.Read(_ => value);

        Assert.True(read.IsError);
        Assert.Equal("SND-CLI-003", read.Error.Code);
        Assert.Contains(CliTimeout.Variable, read.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 15_000), Trait("Lane", "unit")]
    public async Task A_slow_answer_succeeds_when_no_cap_is_set()
    {
        var ct = TestContext.Current.CancellationToken;
        var clock = new FakeTimeProvider();
        var handler = new LateHandler(clock, TimeSpan.FromSeconds(41));
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: handler);

        var call = client.Get<GetStatusResponse>("/sindri/context/map/review", CancellationToken.None);
        await handler.Started.Task.WaitAsync(ct);
        clock.Advance(TimeSpan.FromSeconds(41));
        var answer = await call;

        Assert.True(answer.IsSuccess);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.CallTimeout);   // HttpClient's timeout runs on real time, so it is read, not waited out (the old default was 10 s)
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("2147483", true)]
    [InlineData("2147484", false)]
    [InlineData("99999999999", false)]
    public void The_cap_is_bounded_to_what_HttpClient_accepts(string value, bool valid)
    {
        var read = CliTimeout.Read(_ => value);

        Assert.Equal(valid, read.IsSuccess);
        if (valid)
        {
            using var http = new HttpClient { Timeout = read.Value!.Value };   // why: valid means a cap
            Assert.Equal(read.Value.Value, http.Timeout);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_malformed_cap_refuses_the_calls_and_never_reaches_the_handler()
    {
        var handler = new LateHandler(new FakeTimeProvider(), TimeSpan.Zero);
        var refusal = CliTimeout.Read(_ => "abc").Error;
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: handler, refusal: refusal);

        var answer = await client.Get<GetStatusResponse>("/sindri/status", TestContext.Current.CancellationToken);

        Assert.Equal("SND-CLI-003", answer.Error.Code);
        Assert.False(handler.Called);
    }

    [Fact(Timeout = 15_000), Trait("Lane", "unit")]
    public async Task A_stream_that_does_not_open_within_the_cap_is_SND_API_002_not_a_crash()
    {
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, timeout: TimeSpan.FromMilliseconds(50), handler: new LateHandler(new FakeTimeProvider(), TimeSpan.FromHours(1)));

        var frames = new List<Kernel.Result<StreamFrame>>();
        await foreach (var frame in client.Stream("/sindri/events", TestContext.Current.CancellationToken))
        {
            frames.Add(frame);
        }

        var error = Assert.Single(frames).Error;
        Assert.Equal("SND-API-002", error.Code);
        Assert.Contains("didn't open /sindri/events", error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 15_000), Trait("Lane", "unit")]
    public async Task A_hung_services_run_start_is_ended_by_the_interrupt()
    {
        var handler = new LateHandler(new FakeTimeProvider(), TimeSpan.FromHours(1));
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: handler);
        var output = new Output(new StringWriter(), new StringWriter(), SliceHarness.Json);
        var verbs = new ServiceVerbs(client, output, new JobFollower(client, output, TimeProvider.System));
        using var interrupt = new CancellationTokenSource();

        var run = verbs.Run(["run", "flaky"], interrupt.Token, CancellationToken.None);
        await handler.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        await interrupt.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
    }

    [Fact(Timeout = 15_000), Trait("Lane", "unit")]
    public async Task A_call_over_the_cap_is_SND_API_002_naming_the_variable()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, timeout: TimeSpan.FromMilliseconds(50), handler: new LateHandler(new FakeTimeProvider(), TimeSpan.FromHours(1)));

        var answer = await client.Get<GetStatusResponse>("/sindri/status", ct);

        Assert.Equal("SND-API-002", answer.Error.Code);
        Assert.Contains("didn't answer /sindri/status within 0 s (AW_SINDRI_CLI_TIMEOUT)", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 15_000), Trait("Lane", "unit")]
    public async Task A_connect_that_times_out_is_SND_API_002_naming_the_connect()
    {
        var ct = TestContext.Current.CancellationToken;
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new ThrowingHandler(new HttpRequestException("connect", new TimeoutException())));

        var answer = await client.Get<GetStatusResponse>("/sindri/status", ct);

        Assert.Equal("SND-API-002", answer.Error.Code);
        Assert.Contains("couldn't connect to 127.0.0.1:7410 within 5 s", answer.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 15_000), Trait("Lane", "integration")]
    public async Task A_closed_port_fails_fast_through_the_real_handler()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:1"), 42, 3), new Ping(42, 3));
        using var client = new ControlPlaneClient(probe, SliceHarness.Json);

        var answer = await client.Get<GetStatusResponse>("/sindri/status", ct);

        Assert.Equal("SND-API-001", answer.Error.Code);   // a refused connect is 'went away'; only a connect that times out is SND-API-002
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cancelling_ends_an_in_flight_call()
    {
        using var source = new CancellationTokenSource();
        using var client = new ControlPlaneClient(_probe, SliceHarness.Json, handler: new LateHandler(new FakeTimeProvider(), TimeSpan.FromHours(1)));

        var call = client.Get<GetStatusResponse>("/sindri/status", source.Token);
        await source.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
    }

    private sealed class LateHandler(TimeProvider clock, TimeSpan delay) : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Called { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Called = true;
            _ = Started.TrySetResult();
            await Task.Delay(delay, clock, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(FollowRig.Status(withProblem: false)) };
        }
    }

    private sealed class ThrowingHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(failure);
    }
}
