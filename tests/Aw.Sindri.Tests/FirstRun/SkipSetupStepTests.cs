using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SkipSetupStep;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>SkipSetupStep (first-run spec §3.1): only the accounts step (`sources`) and the hello-sindri step (`first-workflow`) can be skipped; a skip sets a scalar key of the live profile and an undo removes it.</summary>
public sealed class SkipSetupStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData(SetupStepId.Sources)]
    [InlineData(SetupStepId.FirstWorkflow)]
    public async Task A_step_that_may_be_skipped_answers_with_its_new_state(SetupStepId step)
    {
        var result = await new SkipSetupStepService().Handle(new SkipSetupStepRequest(step, Skipped: true), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal((step, true), (result.Value.Step, result.Value.Skipped));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(SetupStepId.Repo)]
    [InlineData(SetupStepId.Serve)]
    [InlineData(SetupStepId.Mcp)]
    public async Task A_step_that_may_not_be_skipped_is_refused_as_invalid_input(SetupStepId step)
    {
        var result = await new SkipSetupStepService().Handle(new SkipSetupStepRequest(step, Skipped: true), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }
}
