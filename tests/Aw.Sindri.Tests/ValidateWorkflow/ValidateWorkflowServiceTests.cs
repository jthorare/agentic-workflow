using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Features.ValidateWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.ValidateWorkflow;

/// <summary>'sindri wf validate --slug': the activation checks over a stored revision or a given graph, writing nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class ValidateWorkflowServiceTests : IDisposable
{
    private const string NoTrigger = "{\"version\":1,\"name\":\"No trigger\",\"nodes\":[{\"id\":\"tell\",\"type\":\"notify\",\"config\":{\"title\":\"x\"}}],\"edges\":[]}";

    private readonly WorkflowWriteRig _rig = new();
    private readonly ValidateWorkflowService _service;

    public ValidateWorkflowServiceTests()
    {
        _service = new ValidateWorkflowService(_rig.Workflows, _rig.Checker);
    }

    public void Dispose() => _rig.Dispose();

    [Fact]
    public async Task A_stored_revision_that_passes_answers_its_hash()
    {
        await _rig.Seed();

        var result = await _service.Handle(new ValidateWorkflowRequest(Slug, Revision: null, Spec: null), Token);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Passed);
        Assert.Equal(GraphJson.Hash(SampleGraph()), result.Value.Hash);
    }

    [Fact]
    public async Task A_graph_with_errors_lists_findings_with_a_fix_and_no_hash()
    {
        var result = await _service.Handle(new ValidateWorkflowRequest(Slug: null, Revision: null, NoTrigger), Token);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Passed);
        Assert.Null(result.Value.Hash);
        var finding = result.Value.Findings.First(static f => f.Severity == "error");
        Assert.False(string.IsNullOrWhiteSpace(finding.Fix));
    }

    [Fact]
    public async Task Both_a_slug_and_a_spec_or_neither_is_invalid()
    {
        var both = await _service.Handle(new ValidateWorkflowRequest(Slug, Revision: null, NoTrigger), Token);
        var neither = await _service.Handle(new ValidateWorkflowRequest(Slug: null, Revision: null, Spec: null), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, both.Error.Code);
        Assert.Equal(SindriErrors.InvalidInput.Code, neither.Error.Code);
    }

    [Fact]
    public async Task An_unknown_workflow_or_revision_is_not_found()
    {
        await _rig.Seed();

        var workflow = await _service.Handle(new ValidateWorkflowRequest(Aw.Sindri.Core.Graph.Ids.WorkflowSlug.From("nope"), Revision: null, Spec: null), Token);
        var revision = await _service.Handle(new ValidateWorkflowRequest(Slug, Revision: 9, Spec: null), Token);

        Assert.Equal(SindriErrors.GraphNotFound.Code, workflow.Error.Code);
        Assert.Equal(SindriErrors.GraphNotFound.Code, revision.Error.Code);
    }
}
