using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.ActivateWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.ActivateWorkflow;

/// <summary>'sindri wf activate': a revision that passes the activation checks becomes the one triggers run.</summary>
[Lane(TestLane.Unit)]
public sealed class ActivateWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();
    private readonly ActivateWorkflowService _service;

    public ActivateWorkflowServiceTests()
    {
        _service = new ActivateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Log);
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task The_newest_revision_is_activated_by_default_and_the_event_is_written()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, Graph(), "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal(2, (await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        _ = Assert.Single(_rig.Recorded(EventTypes.WorkflowActivated));
    }

    [Fact]
    public async Task A_named_revision_is_activated()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, Graph(), "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, 1, "owner"), Token);

        Assert.Equal(1, result.Value.Revision);
    }

    [Fact]
    public async Task A_graph_with_errors_is_refused_with_its_findings_and_stays_off()
    {
        await _rig.Seed();
        const string noTrigger = "{\"version\":1,\"name\":\"No trigger\",\"nodes\":[{\"id\":\"tell\",\"type\":\"notify\",\"config\":{\"title\":\"x\"}}],\"edges\":[]}";
        _ = (await _rig.Workflows.Save(Id, 1, Core.Graph.GraphJson.Parse(noTrigger).Value, "owner", Token)).Value;

        var result = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphFindings.Code, result.Error.Code);
        Assert.Null((await _rig.Workflows.Get(Id, Token)).Value.ActiveRevision);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowActivated));
    }

    [Fact]
    public async Task An_unknown_workflow_or_revision_is_not_found_and_an_archived_workflow_is_refused()
    {
        await _rig.Seed();
        var missing = await _service.Handle(new ActivateWorkflowRequest(Aw.Sindri.Core.Graph.Ids.WorkflowSlug.From("nope"), Revision: null, "owner"), Token);
        var revision = await _service.Handle(new ActivateWorkflowRequest(Slug, 9, "owner"), Token);
        _ = (await _rig.Workflows.Archive(Id, Token)).Value;

        var archived = await _service.Handle(new ActivateWorkflowRequest(Slug, Revision: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, missing.Error.Code);
        Assert.Equal(SindriErrors.GraphNotFound.Code, revision.Error.Code);
        Assert.Equal(SindriErrors.GraphArchived.Code, archived.Error.Code);
    }
}
