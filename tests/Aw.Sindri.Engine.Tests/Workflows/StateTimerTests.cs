using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The state timers on the time-skipping server (engine spec §3.4, §3.8, §5.5, W20, X58; engine P5 unit (a)): a wait
/// state's timeout parks, escalates, or retries once and then parks; the dev profile's 1m desk timeout parks
/// 'workflow.dev-timeout' and 'answer --resume' gives it the full minute again on the same run; a timer that ran out
/// while no worker ran fires once the worker is back, and parks once.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class StateTimerTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_wait_state_timeout_with_then_park_retries_once_and_the_second_timeout_parks_timeout()
    {
        var (parked, events) = await Expire("park", "T-41", static v => v.State == "parked", TimeSpan.FromMinutes(21), rows: 3);   // owner q93: the first timeout at 10m retries, the second at 20m parks; the history replays

        Assert.Equal((ParkReason.Timeout, "waiting ran past 10m"), (parked.Question?.Reason, parked.Question?.Question));
        Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], events);
    }

    [Fact]
    public async Task A_wait_state_timeout_with_then_escalate_escalates()
    {
        var (escalated, events) = await Expire("escalate", "T-42", static v => v.State == "escalated");

        Assert.Equal("waiting ran past 10m", escalated.Question?.Question);
        Assert.Equal(["workflow.started", "workflow.transition"], events);
    }

    [Fact]
    public async Task A_retry_rearms_once_and_the_second_timeout_parks()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.timer-retry", Timed("retry"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.timer-retry", "T-43", ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.State == "waiting");

            await env.DelayAsync(TimeSpan.FromMinutes(11), ct);   // the first 10m timeout fired at 10m and re-armed once
            var once = await Rows(desk, 2);
            await env.DelayAsync(TimeSpan.FromMinutes(8), ct);   // 19 m in; the retry timer fires at 20 m
            _ = await desk.Until(static v => v.State == "waiting");
            var stillOnce = await Rows(desk, 2);
            await env.DelayAsync(TimeSpan.FromMinutes(2), ct);   // 21 m in
            var parked = await desk.Until(static v => v.State == "parked");

            Assert.Equal(["workflow.started", "workflow.transition"], once);
            Assert.Equal(["workflow.started", "workflow.transition"], stillOnce);
            Assert.Equal((ParkReason.Timeout, "waiting ran past 10m"), (parked.Question?.Reason, parked.Question?.Question));
            Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], await Rows(desk, 3));
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task Workflow_dev_timeout_parks_after_the_1m_desk_timeout_and_resume_gives_it_the_full_minute_on_the_same_run()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        var profile = Directory.CreateDirectory(Path.Combine(_harness.Home, "profile")).FullName;
        await File.WriteAllTextAsync(Path.Combine(profile, "profile.yaml"), WorkflowsHarness.Shipped("dev/profile.yaml"), ct);
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-2", ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.Task is not null);

            await env.DelayAsync(TimeSpan.FromSeconds(75), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('3', 32)), new string('0', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
            var resumed = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('3', 32)));
            await env.DelayAsync(TimeSpan.FromSeconds(50), ct);
            var waiting = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromSeconds(15), ct);
            var again = await desk.Until(static v => v.State == "parked");

            Assert.Equal((ParkReason.Timeout, "hold ran past 1m", "1.1"), (parked.Question?.Reason, parked.Question?.Question, parked.Run?.Value));
            Assert.Equal(("hold", "1.1", true), (resumed.Instance.State, resumed.Instance.Run?.Value, resumed.Instance.Task is not null));
            Assert.Equal(("hold", "1.1", null), (waiting.State, waiting.Run?.Value, waiting.Question));
            Assert.Equal((ParkReason.Timeout, "1.1"), (again.Question?.Reason, again.Run?.Value));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task A_takeover_over_a_timeout_park_leaves_the_park_alone_and_resume_afterwards_gives_the_full_minute()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        var profile = Directory.CreateDirectory(Path.Combine(_harness.Home, "profile")).FullName;
        await File.WriteAllTextAsync(Path.Combine(profile, "profile.yaml"), WorkflowsHarness.Shipped("dev/profile.yaml"), ct);
        _harness.Recipe("workflow.dev-timeout", WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", "T-46", ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromSeconds(75), ct);
            var parked = await desk.Until(static v => v.State == "parked");
            var rows = await Rows(desk, 2);

            var taken = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(new TakeoverRequested("human:cli", Stamp('1'))), new WorkflowUpdateOptions(new string('1', 32)));
            await env.DelayAsync(TimeSpan.FromHours(3), ct);
            var back = await handle.ExecuteUpdateAsync(wf => wf.HandbackAsync(new HandbackRequested("human:cli", Stamp('2'))), new WorkflowUpdateOptions(new string('2', 32)));
            var resume = new AnswerGiven("human:cli", Stamp('3'), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
            var resumed = await handle.ExecuteUpdateAsync(wf => wf.AnswerAsync(resume), new WorkflowUpdateOptions(new string('3', 32)));
            await env.DelayAsync(TimeSpan.FromSeconds(50), ct);
            var waiting = await desk.Until(static v => v.Task is not null);
            await env.DelayAsync(TimeSpan.FromSeconds(15), ct);
            var again = await desk.Until(static v => v.State == "parked");

            Assert.Equal(["workflow.started", "workflow.transition"], rows);
            Assert.Equal(("taken-over", null), (taken.Instance.State, taken.Instance.Paused));
            Assert.Equal((ParkReason.Timeout, "hold ran past 1m", "1.1"), (back.Instance.Question?.Reason, back.Instance.Question?.Question, back.Instance.Run?.Value));
            Assert.Null(back.Instance.Paused);
            Assert.Equal(parked.Question, back.Instance.Question);
            Assert.Equal(("hold", "1.1", true), (resumed.Instance.State, resumed.Instance.Run?.Value, resumed.Instance.Task is not null));
            Assert.Equal(("hold", "1.1", null), (waiting.State, waiting.Run?.Value, waiting.Question));
            Assert.Equal((ParkReason.Timeout, "1.1"), (again.Question?.Reason, again.Run?.Value));
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact]
    public async Task Expired_timer_fires_on_resume()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.timer-park", Timed("park"));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var options = sindri.GetRequiredService<TemporalWorkerOptions>();
        var first = (TemporalWorkerOptions)options.Clone();
        first.MaxCachedWorkflows = 0;   // finding F2: the time-skipping server never moves a task off a dead worker's sticky queue
        WorkflowHandle<Infrastructure.Workflows.RecipeWorkflow, Infrastructure.Workflows.RecipeResult>? handle = null;
        using (var before = new TemporalWorker(env.Client, first))
        {
            await before.ExecuteAsync(async () =>
            {
                handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.timer-park", "T-44", ct));
                _ = await WorkflowsHarness.Until(handle, static v => v.State == "waiting");
            }, ct);
        }

        var down = env.DelayAsync(TimeSpan.FromHours(3), ct);   // no worker runs while the server skips past the 10m timeout (X58)
        await TimerFired(handle!, ct); // why: the first worker's run set it before it returned
        using var after = new TemporalWorker(env.Client, options);
        await after.ExecuteAsync(async () =>
        {
            await down;
            var desk = new DeskInstance(sindri, handle!); // why: the first worker's run set it before it returned
            await env.DelayAsync(TimeSpan.FromMinutes(11), ct);   // the expired timer fired once the worker was back and retried; the re-armed one parks (owner q93)
            var parked = await desk.Until(static v => v.State == "parked");
            await env.DelayAsync(TimeSpan.FromHours(1), ct);

            Assert.Equal(ParkReason.Timeout, parked.Question?.Reason);
            Assert.Equal(["workflow.started", "workflow.transition", "workflow.transition"], await Rows(desk, 3));
            Assert.Equal("parked", (await desk.Until(static _ => true)).State);
            await desk.Handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 240_000)]
    public async Task A_takeover_after_a_re_arm_cancels_the_newer_timer_though_the_older_scope_exits_after_it_started()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, "T-47", ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.Task is not null);
            var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
            var report = new StepCompleted("human:cli", RunToken.From("1.1"), OutcomeName.From("done"), new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output }, Tokens: 0, StepRuntime.Desk, new string('f', 64));
            var done = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));   // 'checks' arms its timer while 'produce''s still runs: the older scope exits after the newer one started
            Assert.Equal("-> checks", done.Happened);

            _ = await handle.ExecuteUpdateAsync(wf => wf.TakeoverAsync(new TakeoverRequested("human:cli", Stamp('1'))), new WorkflowUpdateOptions(new string('1', 32)));
            await env.DelayAsync(TimeSpan.FromDays(3), ct);   // past the desk timeout (2d): only 'takeoverIdle' (2h) may fire
            var fired = (await handle.FetchHistoryAsync()).Events.Count(static e => e.EventType == EventType.TimerFired);

            Assert.Equal(1, fired);
            await handle.TerminateAsync("test over");
        }, ct);
    }

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));

    /// <summary>Waits until the instance's history shows a fired timer: it fired server-side while no worker ran.</summary>
    private static async Task TimerFired(WorkflowHandle handle, CancellationToken ct)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        while (RealTime.NowDateTime < deadline)
        {
            var history = await handle.FetchHistoryAsync();
            if (history.Events.Any(static e => e.EventType == EventType.TimerFired))
            {
                return;
            }

            await Task.Delay(100, ct);
        }

        Assert.Fail("the timer never fired while the worker was down");
    }

    /// <summary>Starts the 'then: <paramref name="then"/>' recipe on <paramref name="item"/>, skips past its 10m timeout and reads the result.</summary>
    private async Task<(InstanceView View, IReadOnlyList<string> Events)> Expire(string then, string item, Func<InstanceView, bool> expired, TimeSpan? wait = null, int rows = 2)
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        _harness.Recipe("workflow.timer-" + then, Timed(then));
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        return await worker.ExecuteAsync(async () =>
        {
            var handle = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.timer-" + then, item, ct));
            var desk = new DeskInstance(sindri, handle);
            _ = await desk.Until(static v => v.State == "waiting");

            await env.DelayAsync(wait ?? TimeSpan.FromMinutes(11), ct);
            var view = await desk.Until(expired);
            var events = await Rows(desk, rows);
            Assert.Null((await InterpreterEvalTests.Replay(handle, ct)).ReplayFailure);
            await handle.TerminateAsync("test over");
            return (view, events);
        }, ct);
    }

    /// <summary>The 'workflow.*' ledger rows once there are <paramref name="count"/> (the mirror follows the workflow task).</summary>
    private static async Task<IReadOnlyList<string>> Rows(DeskInstance desk, int count)
    {
        var deadline = RealTime.NowDateTime + TimeSpan.FromSeconds(20);
        var rows = await desk.Events();
        while (rows.Count < count && RealTime.NowDateTime < deadline)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken);
            rows = await desk.Events();
        }

        return rows;
    }

    /// <summary>A recipe whose one wait state times out after 10m with 'then: <paramref name="then"/>'.</summary>
    private static string Timed(string then) => string.Join('\n',
    [
        "schema: 1",
        "id: workflow.timer-" + then,
        "version: 1",
        "inputs: [{ key: fact:item, type: item, kind: item }]",
        "initial: waiting",
        "states:",
        "  waiting: { kind: wait, waitFor: { event: item.routed }, timeout: { after: 10m, then: " + then + " },",
        "             next: \"Waiting for the router to route {item}.\" }",
        "  done:    { kind: terminal, outcome: resolved }",
        "transitions:",
        "  - { from: waiting, to: done, on: wait.satisfied }",
    ]);
}
