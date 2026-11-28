using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Features.ReplaceWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.ReplaceWorkflow;

/// <summary>'sindri wf edit --file': a whole new graph, under the same base-revision rule as ops.</summary>
[Lane(TestLane.Unit)]
public sealed class ReplaceWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new();
    private readonly ReplaceWorkflowService _service;

    public ReplaceWorkflowServiceTests()
    {
        _service = new ReplaceWorkflowService(_rig.Workflows, _rig.Checker, _rig.Log, new StartSession.OpenGate());
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task A_replace_on_the_latest_revision_saves_the_next_one_and_writes_the_revised_event()
    {
        await _rig.Seed();

        var result = await _service.Handle(new ReplaceWorkflowRequest(Slug, 1, GraphJson.Write(SampleGraph()), "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        _ = Assert.Single(_rig.Recorded(EventTypes.WorkflowRevised));
    }

    [Fact]
    public async Task A_stale_base_revision_is_a_conflict_with_the_current_revision_and_overwrites_nothing()
    {
        await _rig.Seed();
        _ = (await _rig.Workflows.Save(Id, 1, SampleGraph(), "owner", Token)).Value;

        var result = await _service.Handle(new ReplaceWorkflowRequest(Slug, 1, GraphJson.Write(SampleGraph()), "session:s1"), Token);

        Assert.Equal(SindriErrors.GraphStaleRevision.Code, result.Error.Code);
        Assert.Equal(ErrorKind.Conflict, result.Error.Kind);
        Assert.Equal(2, result.Error.Extensions!.Single().Value.GetInt32());
        Assert.Equal(2, (await _rig.Workflows.Get(Id, Token)).Value.LatestRevision);
    }

    [Fact]
    public async Task A_graph_that_fails_the_draft_checks_is_refused()
    {
        await _rig.Seed();
        const string bad = "{\"version\":1,\"name\":\"Bad\",\"nodes\":[{\"id\":\"x\",\"type\":\"no_such_block\",\"config\":{}}],\"edges\":[]}";

        var result = await _service.Handle(new ReplaceWorkflowRequest(Slug, 1, bad, "owner"), Token);

        Assert.Equal(SindriErrors.GraphFindings.Code, result.Error.Code);
        Assert.Equal(1, (await _rig.Workflows.Get(Id, Token)).Value.LatestRevision);
    }

    [Fact]
    public async Task Text_that_is_not_a_graph_is_refused()
    {
        await _rig.Seed();

        var result = await _service.Handle(new ReplaceWorkflowRequest(Slug, 1, "not json", "owner"), Token);

        Assert.Equal(SindriErrors.GraphInvalid.Code, result.Error.Code);
    }
}
