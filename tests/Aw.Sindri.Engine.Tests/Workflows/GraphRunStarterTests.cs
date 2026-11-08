using Aw.Kernel.Testing;
using Aw.Sindri.Core;
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

    private static World Build()
    {
        var clock = new FakeTimeProvider(KnownInstants.Epoch);
        var runs = new InMemoryWorkflowRunStore(clock);
        var launcher = new FakeGraphRunLauncher();
        var log = new ListLogger<GraphRunStarter>();
        return new World(new GraphRunStarter(runs, launcher, clock, log), runs, launcher, log);
    }

    private static ValueTask<Aw.Kernel.Result<GraphStart>> Start(World w, int occurrence, GraphConcurrency mode) =>
        w.Starter.Start(new GraphStartRequest(_workflow, _slug, Revision: 3, occurrence, Trigger, mode), Token);

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_start_with_the_same_run_id_is_refused()
    {
        var w = Build();
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
        var w = Build();
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
        var w = Build();
        var first = (await Start(w, 1, GraphConcurrency.OnePerWorkflow)).Value;
        _ = (await w.Runs.SetStatus(first.RunId, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;

        var next = (await Start(w, 2, GraphConcurrency.OnePerWorkflow)).Value;

        Assert.Equal(GraphStartKind.Started, next.Kind);
        Assert.Equal(2, w.Launcher.Launched.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Queue_records_a_trigger_that_arrives_while_a_run_is_in_flight_and_waits_to_start_it()
    {
        var w = Build();
        _ = (await Start(w, 1, GraphConcurrency.Queue)).Value;

        var queued = (await Start(w, 2, GraphConcurrency.Queue)).Value;

        Assert.Equal(GraphStartKind.Queued, queued.Kind);
        _ = Assert.Single(w.Launcher.Launched);
        Assert.Equal(WorkflowRunStatus.Queued, (await w.Runs.Get(queued.RunId, Token)).Value.Status);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unlimited_starts_every_trigger_even_while_a_run_is_in_flight()
    {
        var w = Build();
        _ = (await Start(w, 1, GraphConcurrency.Unlimited)).Value;

        var second = (await Start(w, 2, GraphConcurrency.Unlimited)).Value;

        Assert.Equal(GraphStartKind.Started, second.Kind);
        Assert.Equal(2, w.Launcher.Launched.Count);
    }
}
