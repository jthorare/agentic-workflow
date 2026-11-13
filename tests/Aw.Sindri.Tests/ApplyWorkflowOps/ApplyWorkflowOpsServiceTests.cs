using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ApplyWorkflowOps;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.ApplyWorkflowOps;

/// <summary>'sindri wf edit --ops': ops apply against the revision the caller read, all or none.</summary>
[Lane(TestLane.Unit)]
public sealed class ApplyWorkflowOpsServiceTests : IDisposable
{
    private const string Rename = "[{\"op\":\"rename\",\"node\":\"tell\",\"to\":\"announce\"}]";

    private readonly WorkflowWriteRig _rig = new();
    private readonly ApplyWorkflowOpsService _service;

    public ApplyWorkflowOpsServiceTests()
    {
        _service = new ApplyWorkflowOpsService(_rig.Workflows, _rig.Checker, _rig.Log, new StartSession.OpenGate());
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task Ops_on_the_latest_revision_save_the_next_one_and_write_the_revised_event()
    {
        await _rig.Seed();

        var result = await _service.Handle(new ApplyWorkflowOpsRequest(Slug, 1, Rename, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        var saved = (await _rig.Workflows.GetRevision(Id, 2, Token)).Value;
        Assert.Equal("session:s1", saved.CreatedBy);
        Assert.Contains(saved.Spec.Nodes, static n => n.Id.Value == "announce");
        _ = Assert.Single(_rig.Recorded(EventTypes.WorkflowRevised));
    }

    [Fact]
    public async Task A_stale_base_revision_is_a_conflict_with_the_current_revision_and_overwrites_nothing()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, SampleGraph(), "owner", Token)).Value;

        var result = await _service.Handle(new ApplyWorkflowOpsRequest(Slug, 1, Rename, "session:s1"), Token);

        Assert.Equal(SindriErrors.GraphStaleRevision.Code, result.Error.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(2, result.Error.Extensions!.Single().Value.GetInt32());
        Assert.Equal(2, (await _rig.Workflows.Get(Id, Token)).Value.LatestRevision);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowRevised));
    }

    [Fact]
    public async Task A_failing_op_applies_none_of_the_batch()
    {
        await _rig.Seed();
        const string batch = "[{\"op\":\"rename\",\"node\":\"tell\",\"to\":\"announce\"},{\"op\":\"remove_node\",\"node\":\"missing\"}]";

        var result = await _service.Handle(new ApplyWorkflowOpsRequest(Slug, 1, batch, "owner"), Token);

        Assert.Equal(SindriErrors.GraphOpInvalid.Code, result.Error.Code);
        Assert.Equal(1, (await _rig.Workflows.Get(Id, Token)).Value.LatestRevision);
    }

    [Fact]
    public async Task Ops_that_are_not_a_JSON_array_of_ops_are_refused()
    {
        await _rig.Seed();

        var result = await _service.Handle(new ApplyWorkflowOpsRequest(Slug, 1, "{\"op\":\"rename\"}", "owner"), Token);

        Assert.Equal(SindriErrors.GraphOpInvalid.Code, result.Error.Code);
    }

    [Fact]
    public async Task An_unknown_workflow_is_not_found_and_an_archived_one_is_refused()
    {
        await _rig.Seed();
        var unknown = await _service.Handle(new ApplyWorkflowOpsRequest(Core.Graph.Ids.WorkflowSlug.From("nope"), 1, Rename, "owner"), Token);
        _ = (await _rig.Workflows.Archive(Id, Token)).Value;

        var archived = await _service.Handle(new ApplyWorkflowOpsRequest(Slug, 1, Rename, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, unknown.Error.Code);
        Assert.Equal(SindriErrors.GraphArchived.Code, archived.Error.Code);
    }
}
