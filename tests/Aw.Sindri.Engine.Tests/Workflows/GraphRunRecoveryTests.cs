using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Serve start ends the runs a crash left 'running' with no Temporal workflow and starts the next queued run (issue 2367).</summary>
public sealed class GraphRunRecoveryTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");
    private const string Trigger = "{\"kind\":\"cron\"}";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeProbe : IGraphRunProbe
    {
        private readonly HashSet<string> _known = new(StringComparer.Ordinal);

        public void Know(WorkflowRunId run) => _known.Add(run.Value);

        public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<bool>>(_known.Contains(run.Value));
    }

    private sealed record World(GraphRunRecovery Recovery, InMemoryWorkflowRunStore Runs, FakeGraphRunLauncher Launcher, FakeProbe Probe, SliceHarness Harness);

    private static async Task<World> Build()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        var probe = new FakeProbe();
        return new World(new GraphRunRecovery(runs, probe, mirror, starter, NullLogger<GraphRunRecovery>.Instance), runs, launcher, probe, harness);
    }

    private static WorkflowRun RunOf(int occurrence, WorkflowRunStatus status) =>
        new(WorkflowRunId.Of(_slug, 3, occurrence), _workflow, Revision: 3, Trigger, status, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null);

    /// <summary>Records a run as the store's own admit does, without starting anything (the state a crash before the launch leaves).</summary>
    private static async Task<WorkflowRunId> Admit(World w, int occurrence, GraphConcurrency mode)
    {
        var run = RunOf(occurrence, WorkflowRunStatus.Running);
        _ = (await w.Runs.Admit(run, mode, Token)).Value;
        return run.Id;
    }

    private static async Task<IReadOnlyList<string>> RunRows(World w) =>
        [.. (await w.Harness.Store.ReadAfter(0, 1000, Token)).Value
            .Where(static e => e.Envelope.Type.StartsWith("workflow.run.", StringComparison.Ordinal))
            .Select(static e => $"{e.Envelope.Type} {e.IdempotencyKey}")];

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_row_with_no_temporal_workflow_is_failed_as_lost_at_restart_and_its_queued_run_starts()
    {
        var w = await Build();
        var lost = await Admit(w, 1, GraphConcurrency.Queue);
        var queued = await Admit(w, 2, GraphConcurrency.Queue);
        Assert.Equal(WorkflowRunStatus.Queued, (await w.Runs.Get(queued, Token)).Value.Status);

        var ended = await w.Recovery.Run(Token);

        Assert.Equal(1, ended.Value);
        var row = (await w.Runs.Get(lost, Token)).Value;
        Assert.Equal(WorkflowRunStatus.Failed, row.Status);
        Assert.Contains("lost at restart", row.Error, StringComparison.Ordinal);
        var rows = await RunRows(w);
        Assert.Contains(rows, r => r.StartsWith("workflow.run.started", StringComparison.Ordinal) && r.Contains(lost.Value, StringComparison.Ordinal));
        Assert.Contains(rows, r => r.StartsWith("workflow.run.finished", StringComparison.Ordinal) && r.Contains(lost.Value, StringComparison.Ordinal));
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(queued, Token)).Value.Status);
        Assert.Equal([queued], w.Launcher.Launched.Select(static l => l.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_row_left_by_a_promote_is_ended_the_same_way()
    {
        var w = await Build();
        var first = await Admit(w, 1, GraphConcurrency.Queue);
        var promotedRun = await Admit(w, 2, GraphConcurrency.Queue);
        var behind = await Admit(w, 3, GraphConcurrency.Queue);
        // run 1 ends, the store promotes run 2, and serve dies before the launcher starts it
        _ = (await w.Runs.SetStatus(first, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;
        var promoted = (await w.Runs.PromoteOldestQueued(_workflow, Token)).Value;
        Assert.Equal(promotedRun, promoted?.Id);
        Assert.Empty(w.Launcher.Launched);

        var ended = await w.Recovery.Run(Token);

        Assert.Equal(1, ended.Value);
        var row = (await w.Runs.Get(promotedRun, Token)).Value;
        Assert.Equal(WorkflowRunStatus.Failed, row.Status);
        Assert.Contains("lost at restart", row.Error, StringComparison.Ordinal);
        Assert.Equal([behind], w.Launcher.Launched.Select(static l => l.Id));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_row_temporal_knows_is_left_running()
    {
        var w = await Build();
        var known = await Admit(w, 1, GraphConcurrency.Queue);
        var queued = await Admit(w, 2, GraphConcurrency.Queue);
        w.Probe.Know(known);

        var ended = await w.Recovery.Run(Token);

        Assert.Equal(0, ended.Value);
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(known, Token)).Value.Status);
        Assert.Equal(WorkflowRunStatus.Queued, (await w.Runs.Get(queued, Token)).Value.Status);
        Assert.Empty(w.Launcher.Launched);
        Assert.Empty(await RunRows(w));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_recovery_pass_writes_nothing_new()
    {
        var w = await Build();
        _ = await Admit(w, 1, GraphConcurrency.Queue);
        var queued = await Admit(w, 2, GraphConcurrency.Queue);
        _ = await w.Recovery.Run(Token);
        // the first pass launched the queued run, so Temporal knows it now
        w.Probe.Know(queued);
        var rowsAfterFirst = await RunRows(w);
        var launchedAfterFirst = w.Launcher.Launched.Count;

        var ended = await w.Recovery.Run(Token);

        Assert.Equal(0, ended.Value);
        Assert.Equal(rowsAfterFirst, await RunRows(w));
        Assert.Equal(launchedAfterFirst, w.Launcher.Launched.Count);
        Assert.Equal(1, launchedAfterFirst);
    }
}
