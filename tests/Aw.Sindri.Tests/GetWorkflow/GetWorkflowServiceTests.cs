using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.GetWorkflow;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetWorkflow;

/// <summary>'sindri wf show': the revision a workflow runs, who saved its newest revision, and a refusal for an unknown slug.</summary>
[Lane(TestLane.Unit)]
public sealed class GetWorkflowServiceTests
{
    private static readonly WorkflowId _id = WorkflowId.From("WF-00000000000000000000000000000001");
    private static readonly WorkflowSlug _slug = WorkflowSlug.From("morning-check");

    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly GetWorkflowService _service;

    public GetWorkflowServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _service = new GetWorkflowService(_workflows);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_active_workflow_shows_its_active_revision_and_who_saved_the_newest()
    {
        await Create("Morning check");
        _ = (await _workflows.Activate(_id, 1, Token)).Value;
        _clock.Advance(TimeSpan.FromMinutes(5));
        var newest = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "session:s1", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRequest(_slug), Token);

        Assert.True(result.IsSuccess);
        var shown = result.Value;
        Assert.Equal((1, 1, 2), (shown.Revision, shown.ActiveRevision, shown.LatestRevision));
        Assert.Equal("Morning check", shown.Spec["name"]!.GetValue<string>());
        Assert.Equal(("owner", "session:s1", newest.CreatedAt), (shown.CreatedBy, shown.ChangedBy, shown.ChangedAt));
        Assert.Equal((_id, "Morning check v2"), (shown.Id, shown.Name));
        Assert.Equal((await _workflows.GetRevision(_id, 1, Token)).Value.SpecHash, shown.SpecHash);
    }

    [Fact]
    public async Task A_workflow_with_no_active_revision_shows_its_newest()
    {
        await Create("Morning check");
        _ = (await _workflows.Save(_id, 1, Spec("Morning check v2"), "owner", Token)).Value;

        var result = await _service.Handle(new GetWorkflowRequest(_slug), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        Assert.Null(result.Value.ActiveRevision);
        Assert.Equal("Morning check v2", result.Value.Spec["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_unknown_slug_is_not_found()
    {
        var result = await _service.Handle(new GetWorkflowRequest(WorkflowSlug.From("nope")), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, result.Error.Code);
    }

    private static GraphSpec Spec(string name) =>
        new(Version: 1, name, Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);

    private async Task Create(string name) => _ = (await _workflows.Create(_id, _slug, Spec(name), "owner", Token)).Value;
}
