using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.CreateWorkflow;

/// <summary>'sindri wf create': a spec or a template becomes revision 1, off, with a 'workflow.created' event.</summary>
[Lane(TestLane.Unit)]
public sealed class CreateWorkflowServiceTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new(realBlocks: true);
    private readonly CreateWorkflowService _service;

    public CreateWorkflowServiceTests()
    {
        _service = new CreateWorkflowService(_rig.Workflows, _rig.Checker, _rig.Harness.Ids, _rig.Log, new StartSession.OpenGate());
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task A_template_becomes_revision_1_off_and_writes_the_created_event()
    {
        _ = await _rig.Harness.HoldLease();

        var result = await _service.Handle(new CreateWorkflowRequest(Slug, "manual-notify", Spec: null, "session:s1"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Revision);
        var record = (await _rig.Workflows.Get(result.Value.Id, Token)).Value;
        Assert.Null(record.ActiveRevision);
        Assert.Equal("session:s1", record.CreatedBy);
        var created = Assert.Single(_rig.Recorded(EventTypes.WorkflowCreated));
        Assert.Equal($"workflow.created:{result.Value.Id.Value}", created.IdempotencyKey);
    }

    [Theory]
    [InlineData("docs")]
    [InlineData("blocks")]
    public async Task A_slug_the_docs_and_blocks_routes_use_is_refused_and_writes_nothing(string slug)
    {
        _ = await _rig.Harness.HoldLease();

        var result = await _service.Handle(new CreateWorkflowRequest(WorkflowSlug.From(slug), "manual-notify", Spec: null, "owner"), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowCreated));
    }

    [Fact]
    public async Task A_spec_is_saved_as_given()
    {
        _ = await _rig.Harness.HoldLease();
        var spec = GraphTemplates_Find("daily-digest");

        var result = await _service.Handle(new CreateWorkflowRequest(Slug, Template: null, GraphJson.Write(spec), "owner"), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(GraphJson.Hash(spec), result.Value.SpecHash);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Both_a_template_and_a_spec_or_neither_is_invalid_and_writes_nothing(bool template, bool spec)
    {
        _ = await _rig.Harness.HoldLease();

        var result = await _service.Handle(new CreateWorkflowRequest(Slug, template ? "manual-notify" : null, spec ? "{}" : null, "owner"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_rig.Recorded(EventTypes.WorkflowCreated));
    }

    [Fact]
    public async Task An_unknown_template_names_the_known_ones()
    {
        var result = await _service.Handle(new CreateWorkflowRequest(Slug, "nope", Spec: null, "owner"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Contains("manual-notify", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_graph_that_fails_the_draft_checks_is_refused_and_nothing_is_saved()
    {
        _ = await _rig.Harness.HoldLease();
        const string bad = "{\"version\":1,\"name\":\"Bad\",\"nodes\":[{\"id\":\"x\",\"type\":\"no_such_block\",\"config\":{}}],\"edges\":[]}";

        var result = await _service.Handle(new CreateWorkflowRequest(Slug, Template: null, bad, "owner"), Token);

        Assert.Equal(SindriErrors.GraphFindings.Code, result.Error.Code);
        Assert.Empty((await _rig.Workflows.List(includeArchived: true, Token)).Value);
    }

    [Fact]
    public async Task Without_the_lease_nothing_is_saved()
    {
        var result = await _service.Handle(new CreateWorkflowRequest(Slug, "manual-notify", Spec: null, "owner"), Token);

        Assert.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
        Assert.Empty((await _rig.Workflows.List(includeArchived: true, Token)).Value);
    }

    [Fact]
    public async Task A_taken_slug_is_refused_by_the_store()
    {
        _ = await _rig.Harness.HoldLease();
        _ = await _service.Handle(new CreateWorkflowRequest(Slug, "manual-notify", Spec: null, "owner"), Token);

        var again = await _service.Handle(new CreateWorkflowRequest(Slug, "manual-notify", Spec: null, "owner"), Token);

        Assert.Equal(SindriErrors.GraphSlugTaken.Code, again.Error.Code);
        Assert.Equal($"The short id '{Slug.Value}' is taken by another workflow. Pick another.", again.Error.Message);
        _ = Assert.Single(_rig.Recorded(EventTypes.WorkflowCreated));
    }

    private static GraphSpec GraphTemplates_Find(string name) => Core.Graph.Templates.GraphTemplates.Find(name)!.Spec;   // why: the test names a template that exists
}
