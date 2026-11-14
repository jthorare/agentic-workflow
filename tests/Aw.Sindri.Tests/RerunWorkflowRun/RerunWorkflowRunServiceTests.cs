using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.RerunWorkflowRun;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RerunWorkflowRun;

/// <summary>
/// 'Re-run from here' (Workflows page spec §7.3 and §13, P7, #3204): a new run of the source run's revision that takes the stored output of every step the chosen step does not feed,
/// runs the chosen step and everything after it again, and links back to the source run. The graph is t -> a -> b -> c, with a side branch a -> d.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class RerunWorkflowRunServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("nightly-check");
    private static readonly WorkflowRunId _source = WorkflowRunId.Of(_slug, 1, 1);

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly RecordingStarter _starter;
    private readonly RerunWorkflowRunService _service;

    public RerunWorkflowRunServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _starter = new RecordingStarter(_runs, _clock);
        _service = new RerunWorkflowRunService(_workflows, _runs, _starter)
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static NodeId N(string id) => NodeId.From(id);

    [Fact]
    public async Task Rerun_from_b_restores_the_steps_b_does_not_feed_and_reruns_b_and_c()
    {
        await EndedRun();

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["t", "a", "d"], result.Value.Restored);
        Assert.Equal(["b", "c"], result.Value.Rerun);
        Assert.Equal(_source, result.Value.Source);
        Assert.Equal("started", result.Value.Status);
    }

    [Fact]
    public async Task A_succeeded_step_on_a_side_branch_keeps_its_output()
    {
        await EndedRun();

        _ = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        var restored = Assert.Single(_starter.Requests).Restored!;
        Assert.Contains(restored, r => r.NodeId == N("d") && r.Output == "{\"side\":true}");
    }

    [Fact]
    public async Task Restored_rows_are_the_sources_rows_addressed_to_the_new_run()
    {
        await EndedRun();

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        var request = Assert.Single(_starter.Requests);
        var restored = request.Restored!;
        Assert.All(restored, r => Assert.Equal(result.Value.Run, r.RunId));
        Assert.All(restored, r => Assert.Equal(NodeRunStatus.Succeeded, r.Status));
        var a = Assert.Single(restored, r => r.NodeId == N("a"));
        Assert.Equal("{\"n\":1}", a.Output);
        Assert.Equal("{\"cfg\":\"a\"}", a.Input);
        Assert.DoesNotContain(restored, r => r.NodeId == N("b") || r.NodeId == N("c"));
    }

    [Fact]
    public async Task Every_item_row_of_a_repeated_step_is_restored()
    {
        await EndedRun(extra: [Row("a", item: 1, output: "{\"n\":2}"), Row("a", item: 2, output: "{\"n\":3}")]);

        _ = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        var restored = Assert.Single(_starter.Requests).Restored!;
        Assert.Equal([0, 1, 2], restored.Where(r => r.NodeId == N("a")).Select(static r => r.ItemIndex).Order());
    }

    [Fact]
    public async Task The_new_run_walks_the_source_revision_even_when_a_newer_one_is_active()
    {
        await EndedRun();
        _ = (await _workflows.Save(_id, 1, Spec(), "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 2, Token)).Value;

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal(1, Assert.Single(_starter.Requests).Revision);
        Assert.Equal(1, (await _runs.Get(result.Value.Run, Token)).Value.Revision);
    }

    [Fact]
    public async Task The_trigger_links_the_new_run_to_the_source_and_keeps_the_original_trigger()
    {
        await EndedRun();

        _ = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "session:s1"), Token);

        var trigger = JsonNode.Parse(Assert.Single(_starter.Requests).Trigger)!;
        Assert.Equal("rerun", trigger["kind"]!.GetValue<string>());
        Assert.Equal(_source.Value, trigger["of"]!.GetValue<string>());
        Assert.Equal("b", trigger["fromNode"]!.GetValue<string>());
        Assert.Equal("session:s1", trigger["by"]!.GetValue<string>());
        Assert.Equal("api", trigger["original"]!["payload"]!["repo"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_rerun_never_moves_the_manual_run_counter()
    {
        await EndedRun();

        _ = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal(1, (await _runs.LatestOccurrence(_id, "manual", Token)).Value);
    }

    [Fact]
    public async Task Two_reruns_get_two_different_run_ids_and_neither_takes_the_sources()
    {
        await EndedRun();

        var first = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);
        _ = await _runs.SetStatus(first.Value.Run, WorkflowRunStatus.Succeeded, outputs: null, error: null, Token);
        var second = await _service.Handle(new RerunWorkflowRunRequest(_source, "c", Input: null, "owner"), Token);

        Assert.True(second.IsSuccess);
        Assert.Equal(3, new[] { _source, first.Value.Run, second.Value.Run }.Distinct().Count());
    }

    [Fact]
    public async Task A_run_can_be_re_run_from_the_step_that_failed()
    {
        await EndedRun(status: WorkflowRunStatus.Failed, failedNode: "b");

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["b", "c"], result.Value.Rerun);
    }

    [Fact]
    public async Task The_chosen_input_replaces_the_steps_stored_input_in_the_trigger()
    {
        await EndedRun();

        _ = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", "{\"repo\":\"web\"}", "owner"), Token);

        var trigger = JsonNode.Parse(Assert.Single(_starter.Requests).Trigger)!;
        Assert.Equal("web", trigger["input"]!["repo"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_input_that_is_not_a_JSON_object_is_refused_before_anything_starts()
    {
        await EndedRun();

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", "[1]", "owner"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphBadPayload.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task An_unknown_run_is_SND_GRAPH_101()
    {
        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRunNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_step_the_run_s_revision_does_not_have_is_refused()
    {
        await EndedRun();

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "zzz", Input: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphInvalid.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_run_that_has_not_ended_is_SND_GRAPH_125()
    {
        await EndedRun(status: WorkflowRunStatus.Running);

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRerunNotEnded.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_step_before_the_chosen_one_that_failed_is_SND_GRAPH_126()
    {
        await EndedRun(status: WorkflowRunStatus.Failed, failedNode: "a");

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "c", Input: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphRerunUpstreamNotDone.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_restored_output_the_store_cut_at_64_KB_is_SND_GRAPH_108()
    {
        await EndedRun(extra: [Row("a", item: 0, output: "{\"cut\":", truncated: true)], replaceItem0Of: "a");

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphOutputTruncated.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_deleted_workflow_s_run_can_not_be_re_run()
    {
        await EndedRun();
        _ = (await _workflows.Archive(_id, Token)).Value;

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_busy_one_per_workflow_run_makes_the_rerun_queue_and_the_answer_says_so()
    {
        await EndedRun();
        _starter.Answer = GraphStartKind.Queued;

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, "b", Input: null, "owner"), Token);

        Assert.Equal("queued", result.Value.Status);
    }

    [Theory]
    [InlineData("body")]
    [InlineData("gather")]
    public async Task A_step_inside_a_for_each_body_is_refused_and_the_message_names_the_for_each_step(string step)
    {
        var spec = new GraphSpec(Version: 1, Name: "Loop", Description: null, Concurrency: null, Resources: null,
            Nodes: [new GraphNode(N("t"), "manual", Title: null, []), new GraphNode(N("each"), "for_each", Title: null, []), new GraphNode(N("body"), "http", Title: null, []),
                new GraphNode(N("gather"), "collect", Title: null, []), new GraphNode(N("after"), "http", Title: null, [])],
            Edges: [new GraphEdge(N("t"), Output: null, N("each")), new GraphEdge(N("each"), Output: null, N("body")), new GraphEdge(N("body"), Output: null, N("gather")), new GraphEdge(N("gather"), Output: null, N("after"))]);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _ = (await _runs.Insert(new WorkflowRun(_source, _id, 1, "{\"kind\":\"manual\"}", WorkflowRunStatus.Succeeded, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), _clock.GetUtcNow()), Token)).Value;

        var result = await _service.Handle(new RerunWorkflowRunRequest(_source, step, Input: null, "owner"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.GraphInvalid.Code, result.Error.Code);
        Assert.Contains("re-run from 'each' instead", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_starter.Requests);
    }

    private static GraphSpec Spec() => new(Version: 1, Name: "Nightly check", Description: null, Concurrency: null, Resources: null,
        Nodes: [new GraphNode(N("t"), "manual", Title: null, []), new GraphNode(N("a"), "http", Title: null, []), new GraphNode(N("b"), "http", Title: null, []),
            new GraphNode(N("c"), "http", Title: null, []), new GraphNode(N("d"), "http", Title: null, [])],
        Edges: [new GraphEdge(N("t"), Output: null, N("a")), new GraphEdge(N("a"), Output: null, N("b")), new GraphEdge(N("b"), Output: null, N("c")), new GraphEdge(N("a"), Output: null, N("d"))]);

    private NodeRun Row(string node, int item, string output, bool truncated = false, NodeRunStatus status = NodeRunStatus.Succeeded) =>
        new(_source, N(node), item, status, Input: "{\"cfg\":\"" + node + "\"}", output, truncated, Error: null, Usage: null);

    // A run of revision 1 that ended (or not), with one row per step: t, a, d succeeded; b and c per the arguments.
    private async Task EndedRun(WorkflowRunStatus status = WorkflowRunStatus.Succeeded, string? failedNode = null, IReadOnlyList<NodeRun>? extra = null, string? replaceItem0Of = null)
    {
        _ = (await _workflows.Create(_id, _slug, Spec(), "owner", Token)).Value;
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        var trigger = "{\"kind\":\"manual\",\"payload\":{\"repo\":\"api\"},\"by\":\"owner\"}";
        var ended = status is WorkflowRunStatus.Running ? (DateTimeOffset?)null : _clock.GetUtcNow();
        _ = (await _runs.Insert(new WorkflowRun(_source, _id, 1, trigger, status, Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), ended), Token)).Value;
        var rows = new List<NodeRun>
        {
            Row("t", 0, "{\"kind\":\"manual\"}"), Row("a", 0, "{\"n\":1}"), Row("d", 0, "{\"side\":true}"),
            Row("b", 0, "{\"b\":1}", status: failedNode == "b" ? NodeRunStatus.Failed : NodeRunStatus.Succeeded),
            Row("c", 0, "{\"c\":1}", status: NodeRunStatus.Succeeded),
        };
        if (failedNode is "a")
        {
            rows[1] = Row("a", 0, output: "null", status: NodeRunStatus.Failed);
        }

        if (replaceItem0Of is { } replaced)
        {
            _ = rows.RemoveAll(r => r.NodeId == N(replaced) && r.ItemIndex == 0);
        }

        rows.AddRange(extra ?? []);
        foreach (var row in rows)
        {
            _ = (await _runs.RecordNodeRun(row, Token)).Value;
        }
    }

    /// <summary>Records the request and, like the real starter, admits the run into the store; a set <see cref="Answer"/> stands in for a queue answer.</summary>
    private sealed class RecordingStarter(InMemoryWorkflowRunStore runs, TimeProvider clock) : IGraphRunStarter
    {
        public List<GraphStartRequest> Requests { get; } = [];

        public GraphStartKind? Answer { get; set; }

        public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
            var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running, Outputs: null, Error: null, ItemId: null, clock.GetUtcNow(), EndedAt: null);
            var admitted = await runs.Admit(run, request.Concurrency, cancellationToken);
            return admitted.IsError ? admitted.Error : new GraphStart(Answer ?? admitted.Value, id);
        }
    }
}
