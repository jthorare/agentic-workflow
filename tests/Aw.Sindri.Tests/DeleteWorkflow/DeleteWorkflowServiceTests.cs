using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.DeleteWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.DeleteWorkflow;

/// <summary>'sindri wf delete': the workflow is archived (hidden and off for good); nothing is removed.</summary>
[Lane(TestLane.Unit)]
public sealed class DeleteWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();
    private readonly DeleteWorkflowService _service;

    public DeleteWorkflowServiceTests()
    {
        _service = new DeleteWorkflowService(_rig.Workflows, _rig.Log);
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task An_archived_workflow_leaves_the_list_turns_off_and_writes_the_event()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Activate(Id, 1, Token)).Value;

        var result = await _service.Handle(new DeleteWorkflowRequest(Slug, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Empty((await _rig.Workflows.List(includeArchived: false, Token)).Value);
        var record = (await _rig.Workflows.Get(Id, Token)).Value;
        Assert.Null(record.ActiveRevision);
        Assert.NotNull(record.ArchivedAt);
        Assert.Equal($"workflow.archived:{Id.Value}", Assert.Single(_rig.Recorded(EventTypes.WorkflowArchived)).IdempotencyKey);
    }

    [Fact]
    public async Task An_unknown_workflow_is_not_found_and_an_archived_one_is_refused()
    {
        await _rig.Seed();
        var unknown = await _service.Handle(new DeleteWorkflowRequest(Aw.Sindri.Core.Graph.Ids.WorkflowSlug.From("nope"), "owner"), Token);
        _ = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        var again = await _service.Handle(new DeleteWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.GraphArchived.Code, again.Error.Code);
    }
}
