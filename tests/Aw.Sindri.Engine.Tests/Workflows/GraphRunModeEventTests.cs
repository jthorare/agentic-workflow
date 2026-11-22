using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>P4a (spec 2026-11-12 §3.2): a test run's ledger events say 'test', so the runs view folds its row with Mode = test.</summary>
[Lane(TestLane.Unit)]
public sealed class GraphRunModeEventTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorkflowRun TestRun(int occurrence) =>
        new(WorkflowRunId.Of(_slug, 1, occurrence), _workflow, 1, """{"kind":"manual"}""", WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, KnownInstants.Epoch, EndedAt: null, WorkflowRunMode.Test);

    [Fact]
    public async Task A_test_run_started_appends_workflow_run_started_with_mode_test()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);

        await mirror.AppendStarted(TestRun(7), Token);

        var started = Assert.Single(harness.Store.Events, static e => e.Envelope.Type == EventTypes.WorkflowRunStarted);
        Assert.Equal(WorkflowRunMode.Test, harness.Events.Payload<WorkflowRunStartedPayload>(started).Value.Mode);
    }

    [Fact]
    public async Task A_test_run_lost_at_restart_is_finished_with_mode_test()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        var mirror = new GraphMirror(harness.Store, harness.Held, harness.Events);
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var starter = new GraphRunStarter(runs, new FakeGraphRunLauncher(), mirror, clock, NullLogger<GraphRunStarter>.Instance);
        var recovery = new GraphRunRecovery(runs, new NoTemporalRuns(), mirror, starter, NullLogger<GraphRunRecovery>.Instance);
        _ = (await runs.Admit(TestRun(7), GraphConcurrency.Unlimited, Token)).Value;

        _ = (await recovery.Run(Token)).Value;

        var finished = Assert.Single(harness.Store.Events, static e => e.Envelope.Type == EventTypes.WorkflowRunFinished);
        Assert.Equal(WorkflowRunMode.Test, harness.Events.Payload<WorkflowRunFinishedPayload>(finished).Value.Mode);
    }

    private sealed class NoTemporalRuns : IGraphRunProbe
    {
        public ValueTask<Result<bool>> Exists(WorkflowRunId run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<bool>>(false);
    }
}
