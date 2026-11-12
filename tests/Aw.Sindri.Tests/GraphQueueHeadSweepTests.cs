using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests;

/// <summary>
/// Issue 3102: when a run ends but StartNextQueued fails with an error that is not "Temporal unreachable" (SQLite busy), the finish completes and the queued run behind it waits for ever.
/// Each sweep pass (<see cref="GraphRunRecovery.Run(DateTimeOffset, CancellationToken)"/>, the call GraphRunRecoveryHost makes every minute) must also start the head of every workflow's queue
/// when that workflow has no running row. The state a failed hand-over leaves is built directly: run A has ended, run B is still 'queued'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class GraphQueueHeadSweepTests
{
    private const string Trigger = "{\"kind\":\"cron\"}";
    private static readonly WorkflowId _nightly = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowId _weekly = WorkflowId.From("WF-00000000000000000000000000000002");
    private static readonly WorkflowSlug _nightlySlug = WorkflowSlug.From("nightly");
    private static readonly WorkflowSlug _weeklySlug = WorkflowSlug.From("weekly");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class KnownProbe : IGraphRunProbe
    {
        private readonly HashSet<string> _known = new(StringComparer.Ordinal);

        public void Know(WorkflowRunId run) => _known.Add(run.Value);

        public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(_known.Contains(run.Value));
    }

    private sealed record World(GraphRunRecovery Recovery, GraphRunStarter Starter, InMemoryWorkflowRunStore Runs, FakeGraphRunLauncher Launcher, KnownProbe Probe, FakeTimeProvider Clock);

    private static async Task<World> Build()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        var probe = new KnownProbe();
        return new World(new GraphRunRecovery(runs, probe, mirror, starter, NullLogger<GraphRunRecovery>.Instance), starter, runs, launcher, probe, clock);
    }

    private static async Task<GraphStart> StartRun(World w, WorkflowId workflow, WorkflowSlug slug, int occurrence) =>
        (await w.Starter.Start(new GraphStartRequest(workflow, slug, Revision: 1, occurrence, Trigger, GraphConcurrency.Queue), Token)).Value;

    /// <summary>Run 1 starts, run 2 queues behind it, then run 1 ends and nothing hands over: the state a failed StartNextQueued leaves.</summary>
    private static async Task<(WorkflowRunId Ended, WorkflowRunId Queued)> EndedWithQueuedBehind(World w, WorkflowId workflow, WorkflowSlug slug)
    {
        var first = await StartRun(w, workflow, slug, 1);
        var second = await StartRun(w, workflow, slug, 2);
        Assert.Equal(GraphStartKind.Started, first.Kind);
        Assert.Equal(GraphStartKind.Queued, second.Kind);
        _ = (await w.Runs.SetStatus(first.RunId, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;
        return (first.RunId, second.RunId);
    }

    private static async Task<int> Sweep(World w)
    {
        w.Clock.Advance(TimeSpan.FromMinutes(10));
        return (await w.Recovery.Run(w.Clock.GetUtcNow() - TimeSpan.FromMinutes(5), Token)).Value;
    }

    [Fact]
    public async Task A_queued_run_left_behind_by_a_failed_hand_over_is_started_by_the_next_sweep()
    {
        var w = await Build();
        var (_, queued) = await EndedWithQueuedBehind(w, _nightly, _nightlySlug);

        _ = await Sweep(w);

        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(queued, Token)).Value.Status);   // red today: the sweep advances only the queues of workflows where it ended a run, so B stays queued
        Assert.Contains(queued, w.Launcher.Launched.Select(static l => l.Id));
    }

    [Fact]
    public async Task A_queued_run_behind_a_running_row_is_left_queued()
    {
        var w = await Build();
        var first = await StartRun(w, _nightly, _nightlySlug, 1);
        var queued = await StartRun(w, _nightly, _nightlySlug, 2);
        w.Probe.Know(first.RunId);   // Temporal still has run 1's workflow

        _ = await Sweep(w);

        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(first.RunId, Token)).Value.Status);
        Assert.Equal(WorkflowRunStatus.Queued, (await w.Runs.Get(queued.RunId, Token)).Value.Status);
        Assert.Equal([first.RunId], w.Launcher.Launched.Select(static l => l.Id));
    }

    [Fact]
    public async Task One_pass_starts_the_queued_head_of_every_workflow_with_nothing_running()
    {
        var w = await Build();
        var (_, nightly) = await EndedWithQueuedBehind(w, _nightly, _nightlySlug);
        var (_, weekly) = await EndedWithQueuedBehind(w, _weekly, _weeklySlug);

        _ = await Sweep(w);

        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(nightly, Token)).Value.Status);   // red today, like the first case
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(weekly, Token)).Value.Status);
        Assert.Contains(nightly, w.Launcher.Launched.Select(static l => l.Id));
        Assert.Contains(weekly, w.Launcher.Launched.Select(static l => l.Id));
    }
}
