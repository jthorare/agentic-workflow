using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.ListWorkflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflows;

/// <summary>'sindri wf list': the workflows that are not archived, by slug, each with its active revision and who saved its newest.</summary>
[Lane(TestLane.Unit)]
public sealed class ListWorkflowsServiceTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryWorkflowStore _workflows;
    private readonly ListWorkflowsService _service;

    public ListWorkflowsServiceTests()
    {
        _workflows = new InMemoryWorkflowStore(_clock);
        _service = new ListWorkflowsService(_workflows, new InMemoryWorkflowRunStore(_clock), _clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task No_workflows_is_an_empty_list()
    {
        var result = await _service.Handle(new ListWorkflowsRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Workflows);
    }

    [Fact]
    public async Task Workflows_are_listed_by_slug_with_the_active_revision_and_who_saved_the_newest()
    {
        var drift = await Create(2, "drift-check", "Drift check");
        _ = await Create(1, "morning-check", "Morning check");
        _ = (await _workflows.Activate(drift, 1, Token)).Value;
        _clock.Advance(TimeSpan.FromMinutes(5));
        _ = (await _workflows.Save(drift, 1, Spec("Drift check"), "session:s1", Token)).Value;

        var result = await _service.Handle(new ListWorkflowsRequest(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(["drift-check", "morning-check"], result.Value.Workflows.Select(static w => w.Slug.Value), StringComparer.Ordinal);
        var first = result.Value.Workflows[0];
        Assert.Equal((1, 2, "session:s1", _clock.GetUtcNow()), (first.ActiveRevision, first.LatestRevision, first.ChangedBy, first.ChangedAt));
        var second = result.Value.Workflows[1];
        Assert.Null(second.ActiveRevision);
        Assert.Equal((1, "owner"), (second.LatestRevision, second.ChangedBy));
    }

    [Fact]
    public async Task An_archived_workflow_is_left_out()
    {
        var gone = await Create(1, "gone", "Gone");
        _ = await Create(2, "kept", "Kept");
        _ = (await _workflows.Archive(gone, Token)).Value;

        var result = await _service.Handle(new ListWorkflowsRequest(), Token);

        Assert.Equal(["kept"], result.Value.Workflows.Select(static w => w.Slug.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task By_default_an_archived_workflow_is_left_out_and_no_row_carries_an_archived_mark()
    {
        var gone = await Create(1, "gone", "Gone");
        _ = await Create(2, "kept", "Kept");
        _ = (await _workflows.Archive(gone, Token)).Value;

        var result = await _service.Handle(new ListWorkflowsRequest(), Token);

        Assert.Equal(["kept"], result.Value.Workflows.Select(static w => w.Slug.Value), StringComparer.Ordinal);
        Assert.All(result.Value.Workflows, static w => Assert.Null(w.Archived));
    }

    [Fact]
    public async Task Asked_with_includeArchived_the_archived_workflow_is_listed_and_marked_and_the_live_one_is_not()
    {
        var gone = await Create(1, "gone", "Gone");
        _ = await Create(2, "kept", "Kept");
        _ = (await _workflows.Archive(gone, Token)).Value;

        var result = await _service.Handle(new ListWorkflowsRequest { IncludeArchived = true }, Token);

        Assert.Equal(["gone:True", "kept:"], [.. result.Value.Workflows.Select(static w => w.Slug.Value + ":" + w.Archived)], StringComparer.Ordinal);
    }

    private static GraphSpec Spec(string name) =>
        new(Version: 1, name, Description: null, Concurrency: null, Resources: null, Nodes: [new GraphNode(NodeId.From("start"), "manual", Title: null, [])], Edges: []);

    private async Task<WorkflowId> Create(int number, string slug, string name)
    {
        var id = WorkflowId.From("WF-" + number.ToString("D32", System.Globalization.CultureInfo.InvariantCulture));
        _ = (await _workflows.Create(id, WorkflowSlug.From(slug), Spec(name), "owner", Token)).Value;
        return id;
    }
}
