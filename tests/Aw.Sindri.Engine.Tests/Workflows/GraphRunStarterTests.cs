using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>A trigger becomes a run, a queued run or a logged skip, and a run id is used once (issue 2258, PR 6).</summary>
public sealed class GraphRunStarterTests
{
    private static readonly WorkflowId _workflow = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("contract-drift");
    private const string Trigger = "{\"kind\":\"cron\"}";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record World(GraphRunStarter Starter, InMemoryWorkflowRunStore Runs, FakeGraphRunLauncher Launcher, ListLogger<GraphRunStarter> Log);

    private static async Task<(GraphMirror Mirror, SliceHarness Harness)> MirrorOf()
    {
        var harness = new SliceHarness();
        _ = await harness.HoldLease();
        return (new GraphMirror(harness.Store, harness.Held, harness.Events), harness);
    }

    private static async Task<World> Build()
    {
        var (mirror, _) = await MirrorOf();
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var log = new ListLogger<GraphRunStarter>();
        return new World(new GraphRunStarter(runs, launcher, mirror, clock, log), runs, launcher, log);
    }

    private static ValueTask<Kernel.Result<GraphStart>> Start(World w, int occurrence, GraphConcurrency mode) =>
        w.Starter.Start(new GraphStartRequest(_workflow, _slug, Revision: 3, occurrence, Trigger, mode), Token);

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_start_with_the_same_run_id_is_refused()
    {
        var w = await Build();
        var first = (await Start(w, 12, GraphConcurrency.Unlimited)).Value;

        var second = await Start(w, 12, GraphConcurrency.Unlimited);

        Assert.Equal(GraphStartKind.Started, first.Kind);
        Assert.Equal("contract-drift@3:12", first.RunId.Value);
        Assert.Equal(SindriErrors.GraphRunIdTaken.Code, second.Error.Code);
        _ = Assert.Single(w.Launcher.Launched);
        _ = Assert.Single((await w.Runs.InFlight(_workflow, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_per_workflow_skips_a_trigger_while_a_run_is_in_flight_and_logs_it()
    {
        var w = await Build();
        _ = (await Start(w, 1, GraphConcurrency.OnePerWorkflow)).Value;

        var skipped = (await Start(w, 2, GraphConcurrency.OnePerWorkflow)).Value;

        Assert.Equal(GraphStartKind.Skipped, skipped.Kind);
        _ = Assert.Single(w.Launcher.Launched);
        _ = Assert.Single((await w.Runs.InFlight(_workflow, Token)).Value);
        Assert.Contains(w.Log.Lines, static l => l.Contains("contract-drift@3:2", StringComparison.Ordinal) && l.Contains("skipped", StringComparison.OrdinalIgnoreCase));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task One_per_workflow_starts_again_once_the_run_in_flight_has_ended()
    {
        var w = await Build();
        var first = (await Start(w, 1, GraphConcurrency.OnePerWorkflow)).Value;
        _ = (await w.Runs.SetStatus(first.RunId, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;

        var next = (await Start(w, 2, GraphConcurrency.OnePerWorkflow)).Value;

        Assert.Equal(GraphStartKind.Started, next.Kind);
        Assert.Equal(2, w.Launcher.Launched.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Queue_records_a_trigger_that_arrives_while_a_run_is_in_flight_and_waits_to_start_it()
    {
        var w = await Build();
        _ = (await Start(w, 1, GraphConcurrency.Queue)).Value;

        var queued = (await Start(w, 2, GraphConcurrency.Queue)).Value;

        Assert.Equal(GraphStartKind.Queued, queued.Kind);
        _ = Assert.Single(w.Launcher.Launched);
        Assert.Equal(WorkflowRunStatus.Queued, (await w.Runs.Get(queued.RunId, Token)).Value.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unlimited_starts_every_trigger_even_while_a_run_is_in_flight()
    {
        var w = await Build();
        _ = (await Start(w, 1, GraphConcurrency.Unlimited)).Value;

        var second = (await Start(w, 2, GraphConcurrency.Unlimited)).Value;

        Assert.Equal(GraphStartKind.Started, second.Kind);
        Assert.Equal(2, w.Launcher.Launched.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_launch_starts_the_next_queued_run_and_the_failed_run_shows_in_the_ledger()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FailFirstLauncher(SindriErrors.GraphInvalid.With("the workflow id is refused"));
        var (mirror, harness) = await MirrorOf();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, new ListLogger<GraphRunStarter>());
        // run 2 arrives while run 1 is in flight, so it queues; then run 1's launch fails
        launcher.WhileLaunching = () => starter.Start(new GraphStartRequest(_workflow, _slug, Revision: 3, Occurrence: 2, Trigger, GraphConcurrency.Queue), Token);

        var first = await starter.Start(new GraphStartRequest(_workflow, _slug, Revision: 3, Occurrence: 1, Trigger, GraphConcurrency.Queue), Token);

        Assert.True(first.IsError);   // the launch error is still the answer
        Assert.Equal(WorkflowRunStatus.Failed, (await runs.Get(WorkflowRunId.Of(_slug, 3, 1), Token)).Value.Status);
        Assert.Equal(WorkflowRunStatus.Running, (await runs.Get(WorkflowRunId.Of(_slug, 3, 2), Token)).Value.Status);
        Assert.Equal([WorkflowRunId.Of(_slug, 3, 1), WorkflowRunId.Of(_slug, 3, 2)], launcher.Launched.Select(static r => r.Id));
        // the failed run is in the ledger: a start and a finish with status failed, so the runs view lists it
        Assert.Equal([EventTypes.WorkflowRunStarted, EventTypes.WorkflowRunFinished], harness.Store.Events.Select(static e => e.Envelope.Type), StringComparer.Ordinal);
        Assert.Equal("workflow.run.finished:contract-drift@3:1", harness.Store.Events[1].IdempotencyKey);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_first_run_whose_launch_finds_temporal_down_leaves_no_row_so_the_same_tick_is_tried_again()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FailFirstLauncher(SindriErrors.EngineUnavailable.With("temporal is down"));
        var (mirror, harness) = await MirrorOf();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, new ListLogger<GraphRunStarter>());
        var request = new GraphStartRequest(_workflow, _slug, Revision: 3, Occurrence: 1, Trigger, GraphConcurrency.OnePerWorkflow);

        var down = await starter.Start(request, Token);
        var again = await starter.Start(request, Token);   // the schedule's next pass: Temporal is back

        Assert.Equal(SindriErrors.EngineUnavailable.Code, down.Error.Code);
        Assert.Equal(GraphStartKind.Started, again.Value.Kind);   // not SND-GRAPH-100: there was no Failed row
        Assert.Equal(WorkflowRunStatus.Running, (await runs.Get(WorkflowRunId.Of(_slug, 3, 1), Token)).Value.Status);
        Assert.Empty(harness.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_temporal_down_the_loop_stops_at_the_first_queued_run_and_puts_it_back_in_the_queue()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FailFirstLauncher(SindriErrors.EngineUnavailable.With("temporal is down"), failEvery: true);
        var (mirror, harness) = await MirrorOf();
        var starter = new GraphRunStarter(runs, launcher, mirror, clock, new ListLogger<GraphRunStarter>());
        foreach (var n in new[] { 2, 3, 4 })
        {
            _ = (await runs.Insert(new WorkflowRun(WorkflowRunId.Of(_slug, 3, n), _workflow, 3, Trigger, WorkflowRunStatus.Queued, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow().AddSeconds(n), EndedAt: null), Token)).Value;
        }

        var advanced = await starter.StartNextQueued(_workflow, Token);

        Assert.Equal(SindriErrors.EngineUnavailable.Code, advanced.Error.Code);   // the finishing run's activity sees an error and is retried
        _ = Assert.Single(launcher.Launched);   // one try, not one per queued run
        var statuses = (await runs.InFlight(_workflow, Token)).Value.Select(static r => r.Status).ToList();
        Assert.Equal([WorkflowRunStatus.Queued, WorkflowRunStatus.Queued, WorkflowRunStatus.Queued], statuses);
        Assert.Empty(harness.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launch_the_launcher_calls_started_keeps_its_row_running_and_the_same_tick_is_one_run()
    {
        var w = await Build();

        var first = await Start(w, 5, GraphConcurrency.OnePerWorkflow);
        var again = await Start(w, 5, GraphConcurrency.OnePerWorkflow);   // the schedule's next pass sees the same tick

        Assert.Equal(GraphStartKind.Started, first.Value.Kind);
        Assert.Equal(SindriErrors.GraphRunIdTaken.Code, again.Error.Code);   // the store refuses the duplicate; Temporal is not asked twice
        _ = Assert.Single(w.Launcher.Launched);
        Assert.Equal(WorkflowRunStatus.Running, (await w.Runs.Get(WorkflowRunId.Of(_slug, 3, 5), Token)).Value.Status);
    }

    /// <summary>Fails with the error the first launch only (every launch with failEvery), after running <see cref="WhileLaunching"/> (which queues a trigger behind the run being launched).</summary>
    private sealed class FailFirstLauncher(Kernel.Error error, bool failEvery = false) : IGraphRunLauncher
    {
        private readonly List<WorkflowRun> _launched = [];

        public IReadOnlyList<WorkflowRun> Launched => _launched;

        public Func<ValueTask<Kernel.Result<GraphStart>>>? WhileLaunching { get; set; }

        public async ValueTask<Kernel.Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken)
        {
            _launched.Add(run);
            if (_launched.Count > 1 && !failEvery)
            {
                return true;
            }

            if (WhileLaunching is { } queue)
            {
                _ = await queue().ConfigureAwait(false);
            }

            return error;
        }
    }
}
