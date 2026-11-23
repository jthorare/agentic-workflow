using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.RerunWorkflowRun;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>
/// Which steps 'Re-run from here' (spec §7.3, #3204) takes from the source run when that run skipped some. The walk writes 'skipped' for a step on a branch nobody took,
/// and also for every step still pending when a failure or a cancel ended the run; only the first kind stays skipped. A loop is taken whole or runs again whole.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RerunRestoreTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000002");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("restore-check");
    private static readonly WorkflowRunId _source = WorkflowRunId.Of(_slug, 1, 1);

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly List<GraphStartRequest> _started = [];
    private readonly RerunWorkflowRunService _service;

    public RerunRestoreTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new RerunWorkflowRunService(_workflows, _runs, new Starter(_runs, _clock, _started));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NodeId N(string id) => NodeId.From(id);

    [Fact]
    public async Task A_step_the_source_skipped_only_because_a_failure_halted_it_runs_again()
    {
        // t -> a -> b -> c, a -> d: b failed first, so the walk skipped c and d while they were still pending
        await Source(Line(), WorkflowRunStatus.Failed, ("t", 0, NodeRunStatus.Succeeded), ("a", 0, NodeRunStatus.Succeeded), ("b", 0, NodeRunStatus.Failed),
            ("c", 0, NodeRunStatus.Skipped), ("d", 0, NodeRunStatus.Skipped));

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["t", "a"], result.Value.Restored);
        Assert.Equal(["b", "c", "d"], result.Value.Rerun);
        Assert.DoesNotContain(Assert.Single(_started).Restored!, r => r.NodeId == N("d"));
    }

    [Fact]
    public async Task A_step_on_a_branch_the_source_did_not_take_stays_skipped()
    {
        // t -> a -(yes)-> b -> c, a -(no)-> d: a took 'yes', so d is dead
        await Source(Branch(), WorkflowRunStatus.Succeeded, ("t", 0, NodeRunStatus.Succeeded), ("a", 0, NodeRunStatus.Succeeded), ("b", 0, NodeRunStatus.Succeeded),
            ("c", 0, NodeRunStatus.Succeeded), ("d", 0, NodeRunStatus.Skipped));

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "c", Input: null, "owner"), Token);

        Assert.Equal(["t", "a", "b", "d"], result.Value.Restored);
        Assert.Equal(["c"], result.Value.Rerun);
        Assert.Contains(Assert.Single(_started).Restored!, r => r.NodeId == N("d") && r.Status == NodeRunStatus.Skipped);
    }

    [Fact]
    public async Task A_loop_with_a_failed_item_runs_again_whole_when_a_sibling_step_is_re_run()
    {
        // t -> each -> body -> gather -> after, t -> side: item 2 of body failed with onFailure continue, and the run went on
        await Source(Loop(), WorkflowRunStatus.Succeeded, ("t", 0, NodeRunStatus.Succeeded), ("each", 0, NodeRunStatus.Succeeded), ("body", 1, NodeRunStatus.Succeeded),
            ("body", 2, NodeRunStatus.Failed), ("gather", 0, NodeRunStatus.Succeeded), ("after", 0, NodeRunStatus.Succeeded), ("side", 0, NodeRunStatus.Succeeded));

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "side", Input: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["t"], result.Value.Restored);
        Assert.Equal(["each", "body", "gather", "after", "side"], result.Value.Rerun);
    }

    [Fact]
    public async Task A_loop_whose_items_all_ended_is_taken_whole()
    {
        await Source(Loop(), WorkflowRunStatus.Succeeded, ("t", 0, NodeRunStatus.Succeeded), ("each", 0, NodeRunStatus.Succeeded), ("body", 1, NodeRunStatus.Succeeded),
            ("body", 2, NodeRunStatus.Skipped), ("gather", 0, NodeRunStatus.Succeeded), ("after", 0, NodeRunStatus.Succeeded), ("side", 0, NodeRunStatus.Succeeded));

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "side", Input: null, "owner"), Token);

        Assert.Equal(["t", "each", "body", "gather", "after"], result.Value.Restored);
        Assert.Equal(["side"], result.Value.Rerun);
    }

    private static GraphNode Node(string id, string type = "http") => new(N(id), type, Title: null, []);

    private static GraphEdge Edge(string from, string to, string? output = null) => new(N(from), output, N(to));

    private static GraphSpec Spec(GraphNode[] nodes, GraphEdge[] edges) => new(Version: 1, Name: "Restore check", Description: null, Concurrency: null, Resources: null, nodes, edges);

    private static GraphSpec Line() => Spec([Node("t", "manual"), Node("a"), Node("b"), Node("c"), Node("d")], [Edge("t", "a"), Edge("a", "b"), Edge("b", "c"), Edge("a", "d")]);

    private static GraphSpec Branch() => Spec([Node("t", "manual"), Node("a", "condition"), Node("b"), Node("c"), Node("d")],
        [Edge("t", "a"), Edge("a", "b", "yes"), Edge("b", "c"), Edge("a", "d", "no")]);

    private static GraphSpec Loop() => Spec([Node("t", "manual"), Node("each", "for_each"), Node("body"), Node("gather", "collect"), Node("after"), Node("side")],
        [Edge("t", "each"), Edge("each", "body"), Edge("body", "gather"), Edge("gather", "after"), Edge("t", "side")]);

    private async Task Source(GraphSpec spec, WorkflowRunStatus status, params (string Node, int Item, NodeRunStatus Status)[] rows)
    {
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _ = (await _runs.Insert(new WorkflowRun(_source, _id, 1, "{\"kind\":\"manual\"}", status, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), _clock.GetUtcNow()), Token)).Value;
        foreach (var (node, item, rowStatus) in rows)
        {
            var output = rowStatus == NodeRunStatus.Succeeded ? "{\"ok\":true}" : null;
            _ = (await _runs.RecordNodeRun(new NodeRun(_source, N(node), item, rowStatus, Input: null, output, Truncated: false, Error: null, Usage: null), Token)).Value;
        }
    }

    /// <summary>Admits the run into the store like the real starter and keeps the request.</summary>
    private sealed class Starter(InMemoryWorkflowRunStore runs, TimeProvider clock, List<GraphStartRequest> started) : IGraphRunStarter
    {
        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            started.Add(request);
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
            var admitted = await runs.Admit(run, request.Concurrency, cancellationToken);
            return admitted.IsError ? admitted.Error : new GraphStart(admitted.Value, id);
        }
    }
}
