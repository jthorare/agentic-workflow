using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Features.RunWorkflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.RunWorkflow;

/// <summary>'sindri wf run': a manual trigger of the active revision, refusing what can't run and passing the starter's answer through.</summary>
[Lane(TestLane.Unit)]
public sealed class RunWorkflowServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly InMemoryWorkflowRunStore _runs;
    private readonly FakeStarter _starter = new();
    private readonly RunWorkflowService _service;

    public RunWorkflowServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _runs = new InMemoryWorkflowRunStore(_clock);
        _service = new RunWorkflowService(_workflows, _runs, _starter);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_manual_run_of_the_active_revision_starts_with_its_payload()
    {
        await Create(concurrency: "queue", activate: true);

        var result = await _service.Handle(new RunWorkflowRequest(_slug, "{\"repo\":\"api\"}", "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("started", result.Value.Status);
        var request = Assert.Single(_starter.Requests);
        Assert.Equal(_id, request.Workflow);
        Assert.Equal(1, request.Revision);
        Assert.Equal(GraphConcurrency.Queue, request.Concurrency);
        var trigger = JsonNode.Parse(request.Trigger)!;
        Assert.Equal("manual", trigger["kind"]!.GetValue<string>());
        Assert.Equal("api", trigger["payload"]!["repo"]!.GetValue<string>());
        Assert.Equal("session:s1", trigger["by"]!.GetValue<string>());
        Assert.Equal(WorkflowRunId.Of(_slug, 1, request.Occurrence), result.Value.Run);
    }

    [Fact]
    public async Task Two_manual_runs_get_two_run_ids()
    {
        await Create(concurrency: null, activate: true);

        var first = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);
        var second = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.Run, second.Value.Run);
    }

    [Fact]
    public async Task An_unknown_workflow_is_refused()
    {
        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_workflow_with_no_active_revision_is_refused()
    {
        await Create(concurrency: null, activate: false);

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotActive.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    [Fact]
    public async Task A_skip_by_one_per_workflow_passes_through_with_its_reason()
    {
        await Create(concurrency: "one-per-workflow", activate: true);
        _starter.Answer = GraphStartKind.Skipped;

        var result = await _service.Handle(new RunWorkflowRequest(_slug, Payload: null, "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("skipped", result.Value.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.Reason));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    public async Task A_payload_that_is_not_a_json_object_is_refused(string payload)
    {
        await Create(concurrency: null, activate: true);

        var result = await _service.Handle(new RunWorkflowRequest(_slug, payload, "owner"), Token);

        Assert.Equal(SindriErrors.GraphBadPayload.Code, result.Error.Code);
        Assert.Empty(_starter.Requests);
    }

    private async Task Create(string? concurrency, bool activate)
    {
        var spec = new GraphSpec(Version: 1, Name: "Morning check", Description: null, Concurrency: concurrency, Resources: null,
            Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);
        _ = (await _workflows.Create(_id, _slug, spec, "owner", Token)).Value;
        if (activate)
        {
            _ = (await _workflows.Activate(_id, 1, Token)).Value;
        }
    }

    private sealed class FakeStarter : IGraphRunStarter
    {
        public List<GraphStartRequest> Requests { get; } = [];

        public GraphStartKind Answer { get; set; } = GraphStartKind.Started;

        public ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult<Result<GraphStart>>(new GraphStart(Answer, WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence)));
        }
    }
}
