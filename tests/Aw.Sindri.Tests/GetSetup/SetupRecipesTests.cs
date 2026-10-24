using Aw.Sindri.Core.Setup;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using static Aw.Sindri.Tests.ApproveAll.ApproveAllFixtures;

namespace Aw.Sindri.Tests.GetSetup;

/// <summary>
/// How many recipes the setup step counts as approved: the registry's reading of a recipe is not enough, because the shipped plan knows whether the SHIPPED hash has a live approval.
/// A recipe the plan still names is not approved, whatever the registry says, and it counts even before anything is registered for it.
/// </summary>
[Trait("Lane", "unit")]
public sealed class SetupRecipesTests
{
    private static readonly ShippedRecipePlan _nothingWaits = new([], []);

    private static RecipeSummary Line(RecipeId id, int version, RecipeStanding standing, params StaleReason[] stale) =>
        new(id, version, Testing.Workflow.Registry.RegistryData.Hash(id.Value + version.ToString(System.Globalization.CultureInfo.InvariantCulture)), standing, standing == RecipeStanding.Approved ? ApprovalVia.Desk : null, stale, [], "work", [], "path", [], []);

    private static ShippedRecipePlan Waiting(params RecipeId[] ids) =>
        new([.. StepAndItem().Concat(FirstShip()).Concat(OneRefused()).DistinctBy(static s => s.Id).Where(s => ids.Contains(s.Id)).Select(Row)], []);

    private static ApproveAllRow Row(ShippedSeed seed) =>
        new(seed.Id, seed.DisplayName, seed.Version, seed.Hash, seed.PreviousApprovedHash, PlanChange.Updated, PlanChangeWords.For(PlanChange.Updated), seed.Capabilities, CapabilityDelta.None, [], seed.Children);

    [Fact]
    public void An_approved_recipe_the_plan_does_not_name_counts_as_approved()
    {
        var fact = SetupRecipes.Count([Line(Step, 2, RecipeStanding.Approved)], _nothingWaits);

        Assert.Equal(new RecipesFact(1, 1), fact);
    }

    [Fact]
    public void A_recipe_approved_at_an_older_hash_is_not_approved_while_the_plan_names_the_newer_one()
    {
        var fact = SetupRecipes.Count([Line(Step, 1, RecipeStanding.Approved)], Waiting(Step));

        Assert.Equal(new RecipesFact(1, 0), fact);
    }

    [Fact]
    public void A_stale_approval_is_not_approved_while_the_plan_names_it()
    {
        var fact = SetupRecipes.Count([Line(Step, 2, RecipeStanding.Approved, StaleReason.PinDrift), Line(Note, 1, RecipeStanding.Approved)], Waiting(Step));

        Assert.Equal(new RecipesFact(2, 1), fact);
    }

    [Fact]
    public void A_shipped_version_that_was_never_registered_counts_and_is_not_approved()
    {
        var fact = SetupRecipes.Count([Line(Step, 2, RecipeStanding.Approved)], Waiting(Item));

        Assert.Equal(new RecipesFact(2, 1), fact);
    }

    [Fact]
    public void A_recipe_waiting_for_its_own_approval_is_not_approved()
    {
        var fact = SetupRecipes.Count([Line(Ship, 2, RecipeStanding.Approved)], new ShippedRecipePlan([], [ShipNeedsItsOwn()]));

        Assert.Equal(new RecipesFact(1, 0), fact);
    }

    [Fact]
    public void A_stale_approval_the_plan_does_not_name_is_not_approved()
    {
        var fact = SetupRecipes.Count([Line(Step, 2, RecipeStanding.Approved, StaleReason.PinDrift)], _nothingWaits);

        Assert.Equal(new RecipesFact(1, 0), fact);
    }

    [Fact]
    public void A_recipe_whose_highest_version_is_revoked_is_still_left_out_when_the_plan_does_not_name_it()
    {
        var fact = SetupRecipes.Count([Line(Step, 1, RecipeStanding.Approved), Line(Step, 2, RecipeStanding.Revoked)], _nothingWaits);

        Assert.Equal(new RecipesFact(0, 0), fact);
    }
}
