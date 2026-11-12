using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// Issue 2999: a graph run must reach a final status in bounded time whatever fault hits its store writes or its workflow code, and then the next run of the workflow starts.
/// Today a lasting store error is retried for ever, an exception in workflow code retries the workflow task for ever, and a stale 'running' row of this process is never swept.
/// Each test lets 30 minutes of server time pass, then waits a bounded real time for the row, so a red run fails on the row still being 'running' and never hangs.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class GraphRunStuckTests
{
    private static readonly TimeSpan _skipped = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan _wait = TimeSpan.FromSeconds(15);
    private static readonly WorkflowId _workflowId = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("sweep-test");
    private const string Trigger = "{\"kind\":\"cron\"}";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public Task A_lasting_error_loading_the_run_fails_it_and_the_queued_run_starts() =>
        FailsAndNextStarts(static c => c is { Op: RunOp.Get });

    [Fact]
    public Task A_lasting_error_marking_the_run_running_fails_it_and_the_queued_run_starts() =>
        FailsAndNextStarts(static c => c is { Op: RunOp.SetStatus, Status: WorkflowRunStatus.Running });

    [Fact]
    public Task A_lasting_error_recording_a_node_fails_the_run_and_the_queued_run_starts() =>
        FailsAndNextStarts(static c => c is { Op: RunOp.RecordNodeRun });

    [Fact]
    public async Task A_one_per_workflow_trigger_after_a_run_hit_a_lasting_error_starts_instead_of_being_skipped()
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(Linear("one-per-workflow"));
        var first = WorkflowRunId.Of(workflow.Slug, 1, 1);
        rig.RunFaults.FailWhen = c => string.Equals(c.Run.Value, first.Value, StringComparison.Ordinal) && c is { Op: RunOp.SetStatus, Status: WorkflowRunStatus.Running };
        _ = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.OnePerWorkflow);

        await rig.Environment.DelayAsync(_skipped);

        var ended = await Settle(rig, first);
        Assert.Equal(WorkflowRunStatus.Failed, ended.Status);   // red today: still Running, so the next trigger is skipped for ever
        var second = await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.OnePerWorkflow);
        Assert.Equal(GraphStartKind.Started, second.Kind);
    }

    [Fact]
    public async Task An_exception_in_workflow_code_ends_the_run_failed_instead_of_retrying_the_workflow_task_for_ever()
    {
        await using var rig = await GraphRig.StartAsync();
        // why: the in-memory store keeps whatever spec it is given and the walk reads 'Edges' on every node, so a spec with no edge list throws inside workflow code, not in an activity
        var workflow = await rig.Define(GraphRig.Spec("unlimited", [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo")], edges: null!));
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Unlimited);

        await rig.Environment.DelayAsync(_skipped);

        var ended = await Settle(rig, started.RunId);
        Assert.Equal(WorkflowRunStatus.Failed, ended.Status);
        Assert.False(string.IsNullOrWhiteSpace(ended.Error));
    }

    [Fact]
    public async Task A_run_started_in_this_process_whose_workflow_is_gone_is_failed_by_a_later_pass_and_its_queued_run_starts()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        var recovery = new GraphRunRecovery(runs, new NothingKnownProbe(), mirror, starter, NullLogger<GraphRunRecovery>.Instance);
        var first = (await starter.Start(new GraphStartRequest(_workflowId, _slug, Revision: 3, Occurrence: 1, Trigger, GraphConcurrency.Queue), Token)).Value;
        var second = (await starter.Start(new GraphStartRequest(_workflowId, _slug, Revision: 3, Occurrence: 2, Trigger, GraphConcurrency.Queue), Token)).Value;
        Assert.Equal(GraphStartKind.Started, first.Kind);
        Assert.Equal(GraphStartKind.Queued, second.Kind);

        // 10 minutes later the host's repeated pass asks for runs that started before now - 5 minutes (the margin that stands in for "still launching"); Temporal has no workflow for the run
        clock.Advance(TimeSpan.FromMinutes(10));
        var ended = await recovery.Run(clock.GetUtcNow() - TimeSpan.FromMinutes(5), Token);

        Assert.Equal(1, ended.Value);   // red today: RunningBeforeThisProcess drops every run this process started, so the pass ends nothing
        Assert.Equal(WorkflowRunStatus.Failed, (await runs.Get(first.RunId, Token)).Value.Status);
        Assert.Equal(WorkflowRunStatus.Running, (await runs.Get(second.RunId, Token)).Value.Status);
        Assert.Equal([first.RunId, second.RunId], launcher.Launched.Select(static l => l.Id));
    }

    private static async Task FailsAndNextStarts(Func<RunCall, bool> fault)
    {
        await using var rig = await GraphRig.StartAsync();
        var workflow = await rig.Define(Linear("queue"));
        var first = WorkflowRunId.Of(workflow.Slug, 1, 1);
        rig.RunFaults.FailWhen = c => string.Equals(c.Run.Value, first.Value, StringComparison.Ordinal) && fault(c);
        var started = await rig.Trigger(workflow, occurrence: 1, GraphConcurrency.Queue);
        var next = await rig.Trigger(workflow, occurrence: 2, GraphConcurrency.Queue);

        await rig.Environment.DelayAsync(_skipped);

        var ended = await Settle(rig, started.RunId);
        Assert.Equal(WorkflowRunStatus.Failed, ended.Status);   // red today: the write is retried for ever and the row stays Running
        Assert.Contains(FaultingRunStore.Message, ended.Error, StringComparison.Ordinal);
        var second = await Settle(rig, next.RunId);
        Assert.Equal(WorkflowRunStatus.Succeeded, second.Status);
    }

    private static GraphSpec Linear(string concurrency) =>
        GraphRig.Spec(concurrency, [GraphRig.Node("start", "manual"), GraphRig.Node("a", "echo")], [GraphRig.Edge("start", "a")]);

    /// <summary>Waits up to 15 s of real time for the run to reach a final status and returns its row either way, so a stuck run fails the caller's assert instead of the wait.</summary>
    private static async Task<WorkflowRun> Settle(GraphRig rig, WorkflowRunId id)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Token);
        limit.CancelAfter(_wait);
        while (true)
        {
            var run = (await rig.Runs.Get(id, Token)).Value;
            if (run.Status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled || limit.IsCancellationRequested)
            {
                return run;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), limit.Token);
            }
            catch (OperationCanceledException) when (limit.IsCancellationRequested && !Token.IsCancellationRequested)
            {
                // the wait is over: the next loop returns the row as it is
            }
        }
    }

    private sealed class NothingKnownProbe : IGraphRunProbe
    {
        public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(false);
    }
}
