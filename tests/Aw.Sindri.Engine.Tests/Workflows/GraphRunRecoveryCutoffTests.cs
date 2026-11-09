using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>Graph-run recovery only ends runs from before the lease, and frees the queues it already ended runs for when it stops on an error (issue 2367).</summary>
public sealed class GraphRunRecoveryCutoffTests
{
    private static readonly WorkflowId _workflowA = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowId _workflowB = WorkflowId.From("WF-00000000000000000000000000000002");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");
    private const string Trigger = "{\"kind\":\"cron\"}";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class ScriptedProbe : IGraphRunProbe
    {
        private int _calls;

        public int FailOnCall { get; init; }

        public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken)
        {
            _calls++;
            return ValueTask.FromResult<Result<bool>>(_calls == FailOnCall ? SindriErrors.EngineUnavailable.With("down") : false);
        }
    }

    private sealed record World(GraphRunRecovery Recovery, InMemoryWorkflowRunStore Runs, FakeGraphRunLauncher Launcher);

    private static async Task<World> Build(IGraphRunProbe probe)
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, NullLogger<GraphRunStarter>.Instance);
        return new World(new GraphRunRecovery(runs, probe, mirror, starter, NullLogger<GraphRunRecovery>.Instance), runs, launcher);
    }

    private static WorkflowRun RunOf(WorkflowId workflow, int occurrence, DateTimeOffset startedAt) =>
        new(WorkflowRunId.Of(_slug, 3, occurrence), workflow, Revision: 3, Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, startedAt, EndedAt: null);

    private static async Task<WorkflowRunId> Admit(World w, WorkflowId workflow, int occurrence, DateTimeOffset startedAt)
    {
        var run = RunOf(workflow, occurrence, startedAt);
        _ = (await w.Runs.Admit(run, GraphConcurrency.Queue, Token)).Value;
        return run.Id;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_row_started_at_or_after_the_cutoff_is_left_running()
    {
        var w = await Build(new ScriptedProbe());
        var cutoff = KnownInstants.Epoch + TimeSpan.FromMinutes(5);
        var before = await Admit(w, _workflowA, 1, cutoff - TimeSpan.FromSeconds(1));
        var atCutoff = await Admit(w, _workflowB, 2, cutoff);

        var ended = await w.Recovery.Run(cutoff, Token);

        Assert.Equal(1, ended.Value);
        Assert.Equal(WorkflowRunStatus.Failed, (await w.Runs.Get(before, Token)).Value.Status);
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(atCutoff, Token)).Value.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_error_on_a_later_row_still_starts_the_queued_run_of_a_workflow_already_freed()
    {
        var w = await Build(new ScriptedProbe { FailOnCall = 2 });
        var first = await Admit(w, _workflowA, 1, KnownInstants.Epoch);
        var queued = await Admit(w, _workflowA, 2, KnownInstants.Epoch + TimeSpan.FromSeconds(1));
        _ = await Admit(w, _workflowB, 3, KnownInstants.Epoch + TimeSpan.FromSeconds(2));

        var result = await w.Recovery.Run(Token);

        Assert.True(result.IsError);
        Assert.Equal(WorkflowRunStatus.Failed, (await w.Runs.Get(first, Token)).Value.Status);
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(queued, Token)).Value.Status);
        Assert.Equal([queued], w.Launcher.Launched.Select(static l => l.Id));
    }
}
