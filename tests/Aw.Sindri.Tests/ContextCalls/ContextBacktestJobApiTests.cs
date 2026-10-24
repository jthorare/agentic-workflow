using System.Net;
using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// 'context eval backtest' as a job route on a started host (context S-7A, API P6): the start answers JobStarted, the engine's backtest runs on the job
/// runtime with the request's arms and window, the job leaves a small result (counts, the pass line and the report path, never a brief), a second
/// start is SND-JOB-409 and a session is refused at the start. The engine's answer is canned: the backtest itself is proven in Aw.Context.Tests.
/// </summary>
public sealed class ContextBacktestJobApiTests : IDisposable
{
    private const string Route = "/sindri/context/eval/backtest";
    private const string ReportPath = "/home/x/context/eval/backtest/20261016T090000Z.json";
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly List<BacktestRequest> _requests = [];
    private TaskCompletionSource? _gate;

    public void Dispose() => _repo.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonElement Json(string body) => JsonDocument.Parse(body).RootElement.Clone();

    private static ArmReport Row(string arm) =>
        new(arm, "source", Leaky: false, NotAvailable: null, new ArmScore(5, 5, 0.5, 0.4, 0.3, 1.0, 0), new ArmPassLine("not met", "not met", "not met", "met", Verdicts.OwnerDecision), [new SampleScore("SPL-101", arm, Unmapped: false, 0.5, 0.4, 0.3, ["file:brief-text.cs"], [], [], Leaky: false)]);

    private static BacktestRun Run() => new(new BacktestReport(1, new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero), "all", 5, SmallSample: true, 1.0, new LeftOut(1, 0, 0, ["SPL-106"]),
        [], [Row("brief"), Row("graph"), new ArmReport("linear", "source", Leaky: false, "S-6", Score: null, PassLine: null, [])], Verdicts.OwnerDecision, ["clusters unavailable: 1 of 5 sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)"]), ReportPath);

    private async Task<GoldenHost> Host(bool blocked = false)
    {
        _gate = blocked ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        return await ContextHosts.Approved(_repo, sessions: false, Token, services => RecordingEngine.Install(services, [], async (request, ct) =>
        {
            _requests.Add(request);
            request.Progress?.Report("corpus: 5 samples");
            if (_gate is { } gate)
            {
                await gate.Task.WaitAsync(ct);
            }

            return Run();
        }));
    }

    private static async Task<JsonElement> Start(GoldenHost host, string body)
    {
        var started = await host.Http.Ask(Post(Route, body, Human(host.Token.Value)));
        Assert.True(started.Status == HttpStatusCode.OK, started.Body);
        return Json(started.Body);
    }

    private static async Task<IReadOnlyList<LedgerEvent>> Ledger(GoldenHost host) =>
        (await host.App.Services.GetRequiredService<ILedgerReader>().ReadAfter(0, 5000, Token)).Value;

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task RunBacktest_job_route_runs_the_engine_with_the_requests_arms_and_leaves_a_small_result()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var started = await Start(host, """{"arms":"brief,graph","since":"2026-07-01T00:00:00Z","limit":10}""");
        await JobHost.Settle(host, Token);
        var events = await Ledger(host);

        Assert.Equal($"/sindri/jobs/{started.GetProperty("jobId").GetString()}", started.GetProperty("links").GetProperty("job").GetString());
        var sent = Assert.Single(_requests);
        Assert.Equal([Arm.Brief, Arm.Graph], sent.Arms);
        Assert.Equal((new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), 10, null), (sent.Since, sent.Limit, sent.Until));
        Assert.Equal(ContextCallerKind.Human, sent.Scope.Caller.Kind);
        var finished = Json(Assert.Single(events, e => e.Envelope.Type == EventTypes.JobFinished).Envelope.Payload);
        Assert.Equal("succeeded", finished.GetProperty("outcome").GetString());
        var result = finished.GetProperty("result");
        Assert.Equal((5, true, 1, ReportPath), (result.GetProperty("samples").GetInt32(), result.GetProperty("smallSample").GetBoolean(), result.GetProperty("leftOut").GetInt32(), result.GetProperty("report").GetString()));
        Assert.Equal(Verdicts.OwnerDecision, result.GetProperty("passLine").GetString());
        Assert.Equal(["brief", "graph", "linear"], result.GetProperty("arms").EnumerateArray().Select(a => a.GetProperty("arm").GetString()).ToList());
        Assert.DoesNotContain("brief-text", finished.GetRawText(), StringComparison.Ordinal);   // counts, the pass line and the path: the samples stay in the report file
        Assert.Contains(events.Where(e => e.Envelope.Type == EventTypes.JobProgress), e => e.Envelope.Payload.Contains("corpus: 5 samples", StringComparison.Ordinal));
        var view = Json((await host.Http.Ask(Get($"/sindri/jobs/{started.GetProperty("jobId").GetString()}", Human(host.Token.Value)))).Body);
        Assert.Equal(("succeeded", "RunContextBacktest"), (view.GetProperty("state").GetString(), view.GetProperty("kind").GetString()));
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task RunBacktest_second_start_while_one_runs_is_SND_JOB_409_naming_the_running_job()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(blocked: true);
        var first = await Start(host, "{}");

        var second = await host.Http.Ask(Post(Route, "{}", Human(host.Token.Value)));
        _gate!.SetResult();
        await JobHost.Settle(host, Token);

        Assert.Equal((HttpStatusCode.Conflict, "SND-JOB-409"), (second.Status, second.Code));
        Assert.Equal(first.GetProperty("jobId").GetString(), Json(second.Body).GetProperty("runningJob").GetString());
        _ = Assert.Single(_requests);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task RunBacktest_start_by_a_session_is_allowed_and_starts_a_job()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var started = await host.Http.Ask(Post(Route, "{}", [(SindriHeaders.Caller, "session"), (SindriHeaders.Repo, FixtureMap.Repo)]));   // owner q93 (2026-10-23)

        Assert.Null(started.Code);
        Assert.Contains(await Ledger(host), e => e.Envelope.Type == EventTypes.JobStarted);
    }

    [Fact(Timeout = 90_000), Trait("Lane", "integration")]
    public async Task RunBacktest_unknown_arm_is_refused_at_the_start_as_082_before_any_job_exists()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var refused = await host.Http.Ask(Post(Route, """{"arms":"bogus"}""", Human(host.Token.Value)));

        Assert.Equal(ContextErrors.UnknownArm.Code, refused.Code);
        Assert.Empty(_requests);
        Assert.DoesNotContain(await Ledger(host), e => e.Envelope.Type == EventTypes.JobStarted);
    }

    [Theory(Timeout = 90_000), Trait("Lane", "integration")]
    [InlineData("""{"limit":0}""")]
    [InlineData("""{"limit":-3}""")]
    [InlineData("""{"since":"2026-09-30T00:00:00Z","until":"2026-07-01T00:00:00Z"}""")]
    public async Task RunBacktest_a_non_positive_limit_or_a_window_that_ends_before_it_starts_is_refused_at_the_start(string body)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host();

        var refused = await host.Http.Ask(Post(Route, body, Human(host.Token.Value)));

        Assert.Equal((HttpStatusCode.BadRequest, "SND-CLI-003"), (refused.Status, refused.Code));
        Assert.Empty(_requests);
        Assert.DoesNotContain(await Ledger(host), e => e.Envelope.Type == EventTypes.JobStarted);
    }
}
