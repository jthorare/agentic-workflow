using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Features.CreateWorkflow;
using Aw.Sindri.Tests.WorkflowWrites;
using static Aw.Sindri.Tests.WorkflowWrites.WorkflowWriteRig;

namespace Aw.Sindri.Tests.CreateWorkflow;

/// <summary>#3509: a data template needs a repo for its agent step; with none in the profile it is refused with the fix, and a template with no agent step still works.</summary>
[Lane(TestLane.Unit)]
public sealed class CreateWorkflowTemplateRepoTests : IDisposable
{
    private readonly WorkflowWriteRig _rig = new(realBlocks: true);

    public void Dispose() => _rig.Dispose();

    private CreateWorkflowService Service(string? firstRepo) =>
        new(_rig.Workflows, _rig.Checker, _rig.Harness.Ids, _rig.Log, new FixedFirstRepo(firstRepo));

    [Fact]
    public async Task A_data_template_with_no_repo_in_the_profile_is_refused_and_names_the_fix()
    {
        _ = await _rig.Harness.HoldLease();

        var result = await Service(firstRepo: null).Handle(new CreateWorkflowRequest(Slug, "weekday-check", Spec: null, "owner"), Token);

        Assert.True(result.IsError);
        Assert.Contains("sindri repo add", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_template_with_no_agent_step_still_works_with_no_repo()
    {
        _ = await _rig.Harness.HoldLease();

        var result = await Service(firstRepo: null).Handle(new CreateWorkflowRequest(Slug, "manual-notify", Spec: null, "owner"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
    }

    private sealed class FixedFirstRepo(string? name) : IFirstRepo
    {
        public ValueTask<string?> Name(CancellationToken cancellationToken) => ValueTask.FromResult(name);
    }
}
