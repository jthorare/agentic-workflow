using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Temporalio.Api.Enums.V1;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Graph-run recovery never fails a run this process is launching, finishes the events of a run it failed before an append error, and reads a closed Temporal workflow as gone (issue 2367, review fixes).</summary>
public sealed class GraphRunRecoveryLaunchTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");
    private const string Trigger = "{\"kind\":\"cron\"}";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class NothingKnownProbe : IGraphRunProbe
    {
        public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(false);
    }

    /// <summary>A launcher that stops inside Launch until the test lets it go, as a Temporal start that has not answered yet.</summary>
    private sealed class BlockingLauncher : IGraphRunLauncher
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public void Release() => _release.SetResult();

        public async ValueTask<Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken)
        {
            _ = _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    private sealed record World(GraphRunRecovery Recovery, GraphRunStarter Starter, InMemoryWorkflowRunStore Runs, SliceHarness Harness, LeaseGrant Lease);

    private static async Task<World> Build(IGraphRunLauncher launcher)
    {
        var harness = new SliceHarness();
        var grant = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        return new World(new GraphRunRecovery(runs, new NothingKnownProbe(), mirror, starter, NullLogger<GraphRunRecovery>.Instance), starter, runs, harness, grant);
    }

    private static async Task<WorkflowRunId> Admit(World w, int occurrence)
    {
        var run = new WorkflowRun(WorkflowRunId.Of(_slug, 3, occurrence), _workflow, Revision: 3, Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);
        _ = (await w.Runs.Admit(run, GraphConcurrency.Queue, Token)).Value;
        return run.Id;
    }

    private static GraphStartRequest Request(int occurrence, GraphConcurrency mode) => new(_workflow, _slug, Revision: 3, occurrence, Trigger, mode);

    [Fact, Trait("Lane", "unit")]
    public async Task A_queued_run_promoted_and_still_launching_is_not_failed_by_recovery()
    {
        var launcher = new BlockingLauncher();
        var w = await Build(launcher);
        // run 1 is running from before the restart; run 2 waited in the queue with an old start time
        var first = await Admit(w, 1);
        var queued = await Admit(w, 2);
        Assert.Equal(WorkflowRunStatus.Queued, (await w.Runs.Get(queued, Token)).Value.Status);
        _ = (await w.Runs.SetStatus(first, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;
        var promote = Task.Run(async () => await w.Starter.StartNextQueued(_workflow, Token), Token);
        await launcher.Entered.WaitAsync(Token);   // the store says running; the launch has not answered

        var ended = await w.Recovery.Run(Token);

        Assert.Equal(0, ended.Value);
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(queued, Token)).Value.Status);
        _ = await promote;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_admitted_as_running_and_still_launching_is_not_failed_by_recovery()
    {
        var launcher = new BlockingLauncher();
        var w = await Build(launcher);
        var start = Task.Run(async () => await w.Starter.Start(Request(1, GraphConcurrency.Queue), Token), Token);
        await launcher.Entered.WaitAsync(Token);   // a trigger between taking the lease and the host's first pass

        var ended = await w.Recovery.Run(Token);

        Assert.Equal(0, ended.Value);
        var id = WorkflowRunId.Of(_slug, 3, 1);
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(id, Token)).Value.Status);
        launcher.Release();
        _ = await start;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_run_failed_before_its_events_were_appended_gets_them_on_the_next_pass()
    {
        var w = await Build(new FakeGraphRunLauncher());
        var lost = await Admit(w, 1);
        w.Harness.Held.Clear();   // the append throws: this process holds no lease

        var first = await w.Recovery.Run(Token);

        Assert.True(first.IsError);
        Assert.Equal(WorkflowRunStatus.Failed, (await w.Runs.Get(lost, Token)).Value.Status);
        w.Harness.Held.Hold(w.Lease);

        var second = await w.Recovery.Run(Token);

        Assert.True(second.IsSuccess);
        var rows = (await w.Harness.Store.ReadAfter(0, 1000, Token)).Value.Select(static e => e.Envelope.Type).Where(static t => t.StartsWith("workflow.run.", StringComparison.Ordinal)).ToList();
        Assert.Contains(rows, static t => string.Equals(t, "workflow.run.started", StringComparison.Ordinal));
        Assert.Contains(rows, static t => string.Equals(t, "workflow.run.finished", StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(WorkflowExecutionStatus.Running, true)]
    [InlineData(WorkflowExecutionStatus.ContinuedAsNew, true)]
    [InlineData(WorkflowExecutionStatus.Completed, false)]
    [InlineData(WorkflowExecutionStatus.Failed, false)]
    [InlineData(WorkflowExecutionStatus.Canceled, false)]
    [InlineData(WorkflowExecutionStatus.Terminated, false)]
    [InlineData(WorkflowExecutionStatus.TimedOut, false)]
    public void Only_a_workflow_that_can_still_run_counts_as_known(WorkflowExecutionStatus status, bool open) =>
        Assert.Equal(open, TemporalGraphRunProbe.IsOpen(status));
}
