using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.RerunWorkflowRun;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.RunWorkflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>
/// Run numbers (#3204 review): a manual run and a re-run take the next number after every manual run and re-run of the workflow, so neither kind walks into the other's numbers.
/// Before, each kind counted only its own runs: two re-runs took 3 and 4, and the next Run tried 3, then 4, then failed SND-GRAPH-100 on every click.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RunNumberTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000003");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("number-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly RunWorkflowService _run;
    private readonly RerunWorkflowRunService _rerun;

    public RunNumberTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        var starter = new Starter(_runs, _clock);
        _run = new RunWorkflowService(_workflows, _runs, starter, new NoLaunches(), new AsyncLocalCallerContextAccessor(new SequentialIdGenerator()), _clock, new FixtureChecker());
        _rerun = new RerunWorkflowRunService(_workflows, _runs, starter);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_manual_run_after_two_re_runs_takes_the_number_after_them()
    {
        await Create();
        var first = await Manual();
        var second = await Manual();
        var rerunOne = await Rerun(second);
        _ = await Rerun(rerunOne);

        var third = await _run.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.True(third.IsSuccess, third.IsError ? third.Error.Message : null);
        Assert.Equal(WorkflowRunId.Of(_slug, 1, 5), third.Value.Run);
        Assert.Equal(WorkflowRunId.Of(_slug, 1, 1), first);
    }

    [Fact]
    public async Task A_re_run_after_fifty_manual_runs_takes_number_fifty_one()
    {
        await Create();
        for (var occurrence = 1; occurrence <= 50; occurrence++)
        {
            var run = new WorkflowRun(WorkflowRunId.Of(_slug, 1, occurrence), _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), _clock.GetUtcNow());
            _ = (await _runs.Insert(run, Token)).Value;
        }

        var result = await _rerun.Handle(new RerunWorkflowRunRequest(WorkflowRunId.Of(_slug, 1, 50), "start", Input: null, "owner"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        Assert.Equal(WorkflowRunId.Of(_slug, 1, 51), result.Value.Run);
    }

    private async Task Create()
    {
        var spec = new GraphSpec(Version: 1, Name: "Number check", Description: null, Concurrency: "unlimited", Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
    }

    // A manual run that ends at once, so a re-run of it is allowed.
    private async Task<WorkflowRunId> Manual()
    {
        var started = (await _run.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token)).Value;
        _ = (await _runs.SetStatus(started.Run, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;
        return started.Run;
    }

    private async Task<WorkflowRunId> Rerun(WorkflowRunId source)
    {
        var started = (await _rerun.Handle(new RerunWorkflowRunRequest(source, "start", Input: null, "owner"), Token)).Value;
        _ = (await _runs.SetStatus(started.Run, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token)).Value;
        return started.Run;
    }

    private sealed class NoLaunches : IGraphLaunchRuns
    {
        public ValueTask<Result<WorkflowRunId?>> RunOf(LaunchId launch, CancellationToken cancellationToken) => ValueTask.FromResult<Result<WorkflowRunId?>>((WorkflowRunId?)null);
    }

    /// <summary>Admits the run into the store like the real starter; a used id is refused with SND-GRAPH-100 and stores nothing.</summary>
    private sealed class Starter(InMemoryWorkflowRunStore runs, TimeProvider clock) : IGraphRunStarter
    {
        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
            var admitted = await runs.Admit(run, request.Concurrency, cancellationToken);
            return admitted.IsError ? admitted.Error : new GraphStart(admitted.Value, id);
        }
    }
}
