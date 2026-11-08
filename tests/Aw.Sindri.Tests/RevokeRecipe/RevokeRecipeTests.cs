using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Testing.Workflow.Registry;
using Aw.Sindri.Tests.ApproveRecipe;

namespace Aw.Sindri.Tests.RevokeRecipe;

/// <summary>
/// Revoking a recipe (engine spec §4.1, §4.2, W11, §10 'Revoke_parks_running_instances'): the preview lists the cascade,
/// the parents that pin it and the instances that park; the revoke records 'recipe.revoked' for the hash and each
/// generated recipe approved from it, and wakes their instances. A refused revoke records nothing.
/// </summary>
public sealed class RevokeRecipeTests
{
    private static readonly WorkflowInstanceId _running = WorkflowInstanceId.For(new Guid(1, 0, 0, new byte[8]));
    private static readonly WorkflowInstanceId _generated = WorkflowInstanceId.For(new Guid(2, 0, 0, new byte[8]));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task Revoke_records_the_revocation_cascades_to_generated_recipes_and_parks_running_instances()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var (step, fast, item) = await Seed(r);

        var issued = (await r.Issue($"revoke workflow.step@{step.Hash.Display}")).Value;
        var revoked = await r.Run<RevokeRecipeRequest, RevokeRecipeResponse>(
            new RevokeRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), RegistryHarness.Human);

        Assert.True(revoked.IsSuccess, revoked.IsError ? revoked.Error.ToString() : null);
        Assert.Equal($"revoke workflow.step@{step.Hash.Display}", issued.Action);
        var preview = revoked.Value.Preview;
        Assert.Equal([new RecipeRef(fast.Id, fast.Hash)], preview.Cascade);
        Assert.Equal([new RecipeRef(item.Id, item.Hash)], preview.Parents);
        Assert.Equal([_running, _generated], preview.Parks);
        Assert.Equal((step.Hash, "human:cli"), (revoked.Value.Revocation.Hash, revoked.Value.Revocation.RevokedBy));
        Assert.Null(revoked.Value.Revocation.CascadedFrom);
        Assert.Equal([(fast.Hash, (Core.Workflow.Ids.RecipeHash?)step.Hash)], revoked.Value.Cascade.Select(c => (c.Hash, c.CascadedFrom)));
        Assert.Equal([step.Hash, fast.Hash], r.Events(EventTypes.RecipeRevoked).Select(e => r.H.Events.Payload<RecipeRevoked>(e).Value.Hash));
        Assert.Equal([(step.Hash, RecipeWakeKind.Revoked), (fast.Hash, RecipeWakeKind.Revoked)], r.Wake.Sent.Select(n => (n.Hash, n.Kind)));
        Assert.NotNull((await r.Approvals.Revocation(fast.Hash, Token)).Value);
        Assert.Equal(r.Events(EventTypes.RecipeRevoked)[0].Sequence, revoked.Value.Accepted.Sequence);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revoking_an_unapproved_or_revoked_hash_is_refused_before_any_challenge()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var unapproved = await r.Issue($"revoke workflow.step@{step.Hash.Display}");
        _ = await r.Approvals.Approve(RegistryData.Approval(step), Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);
        var again = await r.Issue($"revoke workflow.step@{step.Hash.Display}");

        Assert.Equal((WorkflowErrors.RecipeNotApproved.Code, WorkflowErrors.RecipeRevoked.Code), (unapproved.Error.Code, again.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revoke_retried_after_the_cascade_stopped_partway_finishes_the_cascade()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var (step, fast, _) = await Seed(r);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);   // the root's row committed, then the cascade stopped

        var issued = await r.Issue($"revoke workflow.step@{step.Hash.Display}");
        Assert.True(issued.IsSuccess, issued.IsError ? issued.Error.ToString() : null);
        var revoked = await r.Run<RevokeRecipeRequest, RevokeRecipeResponse>(
            new RevokeRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Value.Challenge, issued.Value.Action)), RegistryHarness.Human);

        Assert.True(revoked.IsSuccess, revoked.IsError ? revoked.Error.ToString() : null);
        Assert.Equal([new RecipeRef(fast.Id, fast.Hash)], revoked.Value.Preview.Cascade);
        Assert.Equal(step.Hash, (await r.Approvals.Revocation(fast.Hash, Token)).Value?.CascadedFrom);
        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, (await r.Issue($"revoke workflow.step@{step.Hash.Display}")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Revoke_retried_after_the_cascade_stopped_below_a_stamped_recipe_finishes_the_cascade()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var (step, fast, _) = await Seed(r);
        var faster = RegistryData.Recipe("workflow.faster", baseRecipe: new RecipeRef(fast.Id, fast.Hash));
        _ = await r.Recipes.Add(faster, Token);
        _ = await r.Approvals.Approve(RegistryData.Approval(faster), Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step), Token);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(fast, step.Hash), Token);   // the cascade stopped after workflow.fast

        var issued = await r.Issue($"revoke workflow.step@{step.Hash.Display}");
        Assert.True(issued.IsSuccess, issued.IsError ? issued.Error.ToString() : null);
        var revoked = await r.Run<RevokeRecipeRequest, RevokeRecipeResponse>(
            new RevokeRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Value.Challenge, issued.Value.Action)), RegistryHarness.Human);

        Assert.True(revoked.IsSuccess, revoked.IsError ? revoked.Error.ToString() : null);
        Assert.Equal([new RecipeRef(faster.Id, faster.Hash)], revoked.Value.Preview.Cascade);
        Assert.Equal(fast.Hash, (await r.Approvals.Revocation(faster.Hash, Token)).Value?.CascadedFrom);
        Assert.Equal(WorkflowErrors.RecipeRevoked.Code, (await r.Issue($"revoke workflow.step@{step.Hash.Display}")).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_resumed_revoke_wakes_the_root_as_its_first_revoker()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var (step, _, _) = await Seed(r);
        _ = await r.Approvals.Revoke(RegistryData.Revocation(step) with { RevokedBy = "human:earlier" }, Token);   // then the cascade stopped
        var issued = (await r.Issue($"revoke workflow.step@{step.Hash.Display}")).Value;

        var resumed = await r.Run<RevokeRecipeRequest, RevokeRecipeResponse>(
            new RevokeRecipeRequest(step.Id, step.Hash, new ChallengeAnswer(issued.Challenge, issued.Action)), RegistryHarness.Human);

        Assert.True(resumed.IsSuccess, resumed.IsError ? resumed.Error.ToString() : null);
        Assert.Equal("human:earlier", Assert.Single(r.Wake.Sent, n => n.Hash == step.Hash).OnBehalfOf);
    }

    /// <summary>workflow.step approved and running; workflow.fast generated from it (freeze) and running; workflow.item pinning it as a child.</summary>
    private static async Task<(RegisteredRecipe Step, RegisteredRecipe Fast, RegisteredRecipe Item)> Seed(RegistryHarness r)
    {
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);
        var fast = RegistryData.Recipe("workflow.fast", baseRecipe: new RecipeRef(step.Id, step.Hash));
        _ = await r.Recipes.Add(fast, Token);
        _ = await r.Approvals.Approve(RegistryData.Approval(fast), Token);
        var item = RegistryData.Recipe("workflow.item");
        _ = await r.Recipes.Add(item, Token);
        _ = await r.Approvals.Approve(RegistryData.Approval(item, new RecipePins([], [new ChildPin(step.Id, step.Hash)])), Token);
        r.Wake.Run(step.Hash, _running);
        r.Wake.Run(fast.Hash, _generated);
        return (step, fast, item);
    }
}
