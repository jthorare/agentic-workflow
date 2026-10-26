using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// A recipe whose Step names an input with no runtime in this build (missing a runtime, or a descriptor only) is refused at the
/// challenge and at the approval with SND-WF-016, and records nothing; with a runtime it approves (engine spec §5.1, E1).
/// </summary>
public sealed class ApproveRecipeStepInputTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Approve_refuses_a_recipe_whose_input_has_no_runtime_with_SND_WF_016()
    {
        await using var r = await Started(static s => s.AddBlockDescriptor(FakeStepInput.Descriptor));
        var recipe = await WithInput(r);

        var issue = await r.Issue("approve workflow.step 6");
        var approve = await r.Run<ApproveRecipeRequest, ApproveRecipeResponse>(
            new ApproveRecipeRequest(recipe.Id, recipe.Hash, new ChallengeAnswer(r.Challenges.Issue(ChallengeAction.Approve(recipe.Id, 6), ChallengeSubject.ForRecipe(recipe.Hash, 6, pinsDigest: null)).Id, "approve workflow.step 6")),
            RegistryHarness.Human);

        foreach (var refused in new[] { issue.Error, approve.Error })
        {
            Assert.Equal(WorkflowErrors.StepInputNoRuntime.Code, refused.Code);
            Assert.Contains("states.checks.step.inputs[0]", refused.Message, StringComparison.Ordinal);
            Assert.Contains(FakeStepInput.Id, refused.Message, StringComparison.Ordinal);
        }

        Assert.Empty(r.Events(Core.Events.EventTypes.RecipeApproved));
        Assert.Null((await r.Approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approve_accepts_a_recipe_whose_input_has_a_runtime()
    {
        await using var r = await Started(static s => s.AddStepInput<FakeStepInput>());
        var recipe = await WithInput(r);

        var approved = await r.Approve(recipe);

        Assert.True(approved.IsSuccess, approved.IsError ? approved.Error.ToString() : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Show_still_previews_a_recipe_whose_input_has_no_runtime()
    {
        await using var r = await Started(static s => s.AddBlockDescriptor(FakeStepInput.Descriptor));
        var recipe = await WithInput(r);
        await using var scope = r.Services.CreateAsyncScope();

        var preview = await scope.ServiceProvider.GetRequiredService<RecipeApprovalPlanner>().Preview(recipe, Token);

        Assert.True(preview.IsSuccess, preview.IsError ? preview.Error.ToString() : null);
    }

    private static async Task<RegistryHarness> Started(Action<IServiceCollection> configure)
    {
        var r = new RegistryHarness(configure);
        _ = await r.H.HoldLease();
        return r;
    }

    /// <summary>The shipped workflow.step recipe with 'input.test.echo' on its Steps, registered under its own hash.</summary>
    private static async Task<RegisteredRecipe> WithInput(RegistryHarness r)
    {
        var shipped = RegistryHarness.Parse(await r.Shipped("sindri/workflow.step.yaml"));
        return await r.Register(ARecipe.WithStepInputs(shipped with { Version = 6 }, FakeStepInput.Id));
    }
}
