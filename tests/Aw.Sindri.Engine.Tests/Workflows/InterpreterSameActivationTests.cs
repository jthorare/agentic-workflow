using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Tests.CompleteStep;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Triggers that reach the interpreter in the same workflow task (engine spec §5.2, §5.3; review run 1, P4 interpreter).
/// The worker is stopped while the Updates are admitted (and the desk deadline passes), so the next worker's first
/// activation carries them all: a report that lands with the desk deadline wins over it, and the Updates validated in
/// the activation of the one that ends the instance are all answered, so the execution completes.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class InterpreterSameActivationTests(SindriTimeSkippingServer server) : IClassFixture<SindriTimeSkippingServer>, IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact(Timeout = 120_000)]
    public async Task A_report_in_the_activation_of_the_desk_deadline_wins_and_leaves_nothing_stale()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var handle = await StartOnDesk(sindri, "T-53", ct);
        var desk = new DeskInstance(sindri, handle);
        var report = Report("1.1", "done");
        using var skipping = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ended = handle.GetResultAsync(rpcOptions: new RpcOptions { CancellationToken = skipping.Token });   // waiting on the result lets the time-skipping server skip to the 2d desk deadline while no worker runs
        await TimerFired(handle, ct);
        await skipping.CancelAsync();   // and no further: the next run's own timeout must not skip
        _ = await Assert.ThrowsAnyAsync<Exception>(() => ended.WaitAsync(TimeSpan.FromSeconds(30), ct));
        var update = handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(report), new WorkflowUpdateOptions(new string('a', 32)));   // waits for a worker
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // the server has it queued (this server can't report an Update 'admitted', so there is nothing to poll for; the assertions below carry the history)

        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var done = await update;
            var checks = await desk.Until(static v => v.State == "checks" && v.Task is not null);
            var next = Report("2.1", "passed");
            var again = await handle.ExecuteUpdateAsync(wf => wf.CompleteAsync(next), new WorkflowUpdateOptions(new string('b', 32)));

            var history = await EventTypes(handle);   // the failure message names the history, since the sleep above can't prove the Update was queued
            Assert.True(done.Happened == "-> checks", $"expected '-> checks', got '{done.Happened}'; history: {history}");
            Assert.True(checks.Run?.Value == "2.1", $"expected run 2.1, got '{checks.Run?.Value}'; history: {history}");
            Assert.True(again.Happened == "-> verify", $"expected '-> verify' (a stale report would have refused it, SND-WF-005), got '{again.Happened}'; history: {history}");
            await handle.TerminateAsync("test over");
        }, ct);
    }

    [Fact(Timeout = 120_000)]
    public async Task Two_cancels_in_one_workflow_task_are_both_answered_and_the_workflow_completes()
    {
        var ct = TestContext.Current.CancellationToken;
        var env = server.Environment;
        await using var sindri = _harness.Sindri();
        await WorkflowsHarness.HoldLease(sindri, ct);
        var handle = await StartOnDesk(sindri, "T-54", ct);
        var first = Settled(handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel('a')), new WorkflowUpdateOptions(new string('a', 32))));   // waits for a worker
        var second = Settled(handle.ExecuteUpdateAsync(wf => wf.CancelAsync(Cancel('b')), new WorkflowUpdateOptions(new string('b', 32))));
        await Task.Delay(TimeSpan.FromSeconds(1), ct);   // the server has both queued (no 'admitted' stage to poll for here; the assertions below carry the history)

        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        await worker.ExecuteAsync(async () =>
        {
            var outcomes = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30), ct);
            var result = await handle.GetResultAsync().WaitAsync(TimeSpan.FromSeconds(30), ct);

            var history = await EventTypes(handle);   // the failure message names the history, since the sleep above can't prove both Updates were queued
            Assert.True(outcomes.Take(2).Count(static o => o is null) == 1, $"one cancel should have ended the instance; history: {history}");
            Assert.True(result.State == "cancelled", $"expected 'cancelled', got '{result.State}'; history: {history}");
        }, ct);
    }

    /// <summary>Starts a 'workflow.step' instance, waits for its desk task, then stops the worker that ran it.</summary>
    private async Task<WorkflowHandle<RecipeWorkflow, RecipeResult>> StartOnDesk(IServiceProvider sindri, string item, CancellationToken ct)
    {
        var env = server.Environment;
        var options = sindri.GetRequiredService<TemporalWorkerOptions>();
        options.MaxCachedWorkflows = 0;   // the time-skipping server never moves a task off a dead sticky queue (TemporalProbes)
        using var first = new TemporalWorker(env.Client, options);
        WorkflowHandle<RecipeWorkflow, RecipeResult>? started = null;
        await first.ExecuteAsync(async () =>
        {
            started = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.StepStart(sindri, item, ct));
            _ = await WorkflowsHarness.Until(started, static v => v.Task is not null);
            await ActivityDone(started, ct);
        }, ct);
        return started ?? throw new InvalidOperationException("the instance did not start");
    }

    /// <summary>Waits until the 'RunStep' activity has completed: time doesn't skip while one is open (spec §11 F1), and the worker mustn't stop mid-activity.</summary>
    private static async Task ActivityDone(WorkflowHandle handle, CancellationToken ct)
    {
        while (!(await handle.FetchHistoryAsync()).Events.Any(static e => e.EventType == Temporalio.Api.Enums.V1.EventType.ActivityTaskCompleted))
        {
            await Task.Delay(100, ct);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(500), ct);   // the workflow task that follows it
    }

    /// <summary>The history's event types in order, for an assertion message.</summary>
    private static async Task<string> EventTypes(WorkflowHandle handle) =>
        string.Join(", ", (await handle.FetchHistoryAsync()).Events.Select(static e => e.EventType));

    /// <summary>Waits until the desk deadline's timer has fired in the history: the next worker's first activation carries it.</summary>
    private static async Task TimerFired(WorkflowHandle handle, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(30));
        while (!(await handle.FetchHistoryAsync()).Events.Any(static e => e.EventType == Temporalio.Api.Enums.V1.EventType.TimerFired))
        {
            await Task.Delay(100, limit.Token);
        }
    }

    private static StepCompleted Report(string run, string outcome)
    {
        var output = new FactValue("fact:sha256:" + new string('e', 64), new string('e', 64), HeadSha: null);
        var outputs = outcome == "done" ? new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = output } : [];
        return new StepCompleted("human:cli", RunToken.From(run), OutcomeName.From(outcome), outputs, Tokens: 0, StepRuntime.Desk, new string('f', 64));
    }

    private static CancelRequested Cancel(char nonce) =>
        new("human:cli", CancelOrigin.Command, new CommandStamp(CommandNonce.From(new string(nonce, 32)), new string(nonce, 64)), Sequence: null, Parent: null);

    /// <summary>The Update's failure, or null when it returned.</summary>
    private static async Task<WorkflowUpdateFailedException?> Settled(Task<CommandResult> update)
    {
        try
        {
            _ = await update;
            return null;
        }
        catch (WorkflowUpdateFailedException failed)
        {
            return failed;
        }
    }
}
