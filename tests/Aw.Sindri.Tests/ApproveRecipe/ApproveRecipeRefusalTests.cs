using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Testing.Workflow.Registry;

namespace Aw.Sindri.Tests.ApproveRecipe;

/// <summary>
/// Refused approvals (engine spec §4.2, §6.2, §8): a stale challenge is SND-RECIPE-009, a caller other than a human at a
/// terminal is API's SND-AUTH-002, a revoked hash is SND-RECIPE-005, a recipe without evals is SND-RECIPE-008. Each
/// records no 'recipe.approved' and no approval row.
/// </summary>
public sealed class ApproveRecipeRefusalTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Human_trigger_on_revoked_recipe_gets_005_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approvals.Approve(RegistryData.Approval(step), Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);
        var stale = r.Challenges.Issue(ChallengeAction.Approve(step.Id, 5), ChallengeSubject.ForRecipe(step.Hash, 5, pinsDigest: null));

        var refused = await Approve(r, step, stale.Id, stale.Action.Text);

        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, refused.Error.Code);
        Assert.Empty(r.Events(EventTypes.RecipeApproved));
        _ = Assert.Single((await r.Approvals.History(step.Id, 5, step.Hash, Token)).Value);
        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, (await r.Issue("approve workflow.step 5")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Recipe_without_evals_is_008_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var bare = await r.Register(RegistryHarness.Parse(step) with { Id = RecipeId.From("workflow.bare"), Evals = [] });

        var refused = await r.Issue("approve workflow.bare 5");

        Assert.Equal(WorkflowErrors.RecipeEvalsFailing.Code, refused.Error.Code);
        Assert.Contains("declares no evals", refused.Error.Message, StringComparison.Ordinal);
        Assert.Null((await r.Approvals.Latest(bare.Id, 5, bare.Hash, Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Generated_recipe_whose_base_was_revoked_is_005_and_records_nothing()
    {
        await using var r = await Started();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approvals.Approve(RegistryData.Approval(step), Token);
        var fast = RegistryData.Recipe("workflow.fast", baseRecipe: new RecipeRef(step.Id, step.Hash));
        _ = await r.Recipes.Add(fast, Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);   // registered while its base held; the base was revoked since

        var refused = await r.Issue("approve workflow.fast 1");

        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, refused.Error.Code);
        Assert.Contains("base workflow.step@", refused.Error.Message, StringComparison.Ordinal);
        Assert.Null((await r.Approvals.Latest(fast.Id, 1, fast.Hash, Token)).Value);
    }

    private static async Task<RegistryHarness> Started()
    {
        var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        return r;
    }

    private static Task<Kernel.Result<ApproveRecipeResponse>> Approve(RegistryHarness r, RegisteredRecipe recipe, ChallengeId challenge, string typed) =>
        r.Run<ApproveRecipeRequest, ApproveRecipeResponse>(new ApproveRecipeRequest(recipe.Id, recipe.Hash, new ChallengeAnswer(challenge, typed)), RegistryHarness.Human);
}
