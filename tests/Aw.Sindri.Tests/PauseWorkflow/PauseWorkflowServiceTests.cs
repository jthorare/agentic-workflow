using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.PauseWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.PauseWorkflow;

/// <summary>'sindri wf pause': the active revision is cleared and every revision stays.</summary>
[Lane(TestLane.Unit)]
public sealed class PauseWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();
    private readonly PauseWorkflowService _service;

    public PauseWorkflowServiceTests()
    {
        _service = new PauseWorkflowService(_rig.Workflows, _rig.Log, _rig.Harness.Callers);
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task A_paused_workflow_has_no_active_revision_keeps_its_revisions_and_writes_the_event()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Activate(Id, 1, Token)).Value;

        var result = await _service.Handle(new PauseWorkflowRequest(Slug, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        var record = (await _rig.Workflows.Get(Id, Token)).Value;
        Assert.Null(record.ActiveRevision);
        Assert.Equal(1, record.LatestRevision);
        _ = Assert.Single(_rig.Recorded(EventTypes.WorkflowPaused));
    }

    [Fact]
    public async Task Pausing_a_workflow_that_is_already_off_still_succeeds()
    {
        await _rig.Seed();

        var result = await _service.Handle(new PauseWorkflowRequest(Slug, "owner"), Token);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Pausing_an_archived_workflow_is_refused_and_writes_no_event()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Archive(Id, Token)).Value;

        var result = await _service.Handle(new PauseWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(SindriErrors.GraphArchived.Code, result.Error.Code);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowPaused));
    }

    [Fact]
    public async Task An_unknown_workflow_is_not_found_and_without_the_lease_nothing_changes()
    {
        var unknown = await _service.Handle(new PauseWorkflowRequest(Core.Graph.Ids.WorkflowSlug.From("nope"), "owner"), Token);
        _ = (await _rig.Workflows.Create(Id, Slug, SampleGraph(), "owner", Token)).Value;
        _ = (await _rig.Workflows.Activate(Id, 1, Token)).Value;

        var down = await _service.Handle(new PauseWorkflowRequest(Slug, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.NotRunning.Code, down.Error.Code);
        Assert.Equal(1, (await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
    }
}
