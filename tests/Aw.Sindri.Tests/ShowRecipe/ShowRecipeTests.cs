using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.ListRecipes;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.ShowRecipe;

/// <summary>
/// 'recipe show' and GET /workflow/recipes/{id}[@{hash}] (API spec §3.8, E-22; query): the recipe as the API's view, by the full hash or a
/// prefix of 8 or more digits whether or not it is approved, or by its bare id for the approved version. The approval preview is
/// what 'recipe approve' prints (<see cref="RegistryHarness.Preview"/>).
/// </summary>
public sealed class ShowRecipeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Show_by_a_hash_prefix_serves_the_view_of_an_unapproved_recipe()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var shown = await r.Show(step.Id, step.Hash.Display);

        Assert.True(shown.IsSuccess, shown.IsError ? shown.Error.ToString() : null);
        Assert.Equal((step.Id, 5, step.Hash), (shown.Value.Id, shown.Value.Version, shown.Value.Hash));
        Assert.Equal("fact:spec", Assert.Single(shown.Value.Inputs).Key);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Show_serves_a_legacy_shape_row_by_hash_and_by_bare_id_with_the_documented_no_kind_placeholder()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var source = LegacyRecipeRowTests.LegacyStep();
        var row = new RegisteredRecipe(RecipeId.From("workflow.step"), 1, RecipeDeclarationsTests.LegacyHash(source),
            RecipeDeclarationsTests.LegacyJson(source).ToJsonString(), Base: null, r.H.Clock.GetUtcNow());
        _ = await r.Recipes.Add(row, TestContext.Current.CancellationToken);
        await r.ApproveAsInThePast(row);

        var byHash = await r.Show(row.Id, row.Hash.Display);
        var byId = await r.Show(row.Id);

        Assert.True(byHash.IsSuccess, byHash.IsError ? byHash.Error.ToString() : null);
        Assert.True(byId.IsSuccess, byId.IsError ? byId.Error.ToString() : null);
        var input = Assert.Single(byHash.Value.Inputs);
        Assert.Equal(("fact:spec", SubjectKinds.NoKind), (input.Key, input.Kind));
        Assert.Equal(FactType.Text, input.Type);
        Assert.Equal(row.Hash, byId.Value.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Bare_id_resolves_to_the_approved_hash()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);

        var byId = await r.Show(step.Id);
        var byHash = await r.Show(step.Id, step.Hash.Value);

        Assert.True(byId.IsSuccess, byId.IsError ? byId.Error.ToString() : null);
        Assert.Equal(step.Hash, byId.Value.Hash);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(byHash.Value, SliceHarness.Json), System.Text.Json.JsonSerializer.Serialize(byId.Value, SliceHarness.Json));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Bare_id_of_a_recipe_that_was_never_approved_is_SND_RECIPE_003()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var shown = await r.Show(step.Id);

        Assert.Equal(WorkflowErrors.RecipeNotApproved.Code, shown.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_approval_preview_is_still_what_approving_shows_with_the_pins_an_approved_recipe_holds()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        var before = await r.Preview(step.Id, step.Hash.Display);
        var approved = (await r.Approve(step)).Value;

        var after = await r.Preview(step.Id, step.Hash.Value);

        Assert.Equal(["step.checks", "step.produce", "step.verify"], before.Value.Pins.Blocks.Select(b => b.Block.Value), StringComparer.Ordinal);
        Assert.Contains("3 declared", before.Value.Evals, StringComparison.Ordinal);
        Assert.Null(before.Value.Diff);
        Assert.Equal(approved.Approval.Pins, after.Value.Pins);
        Assert.False(after.Value.IsDrift);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow.step", "0000000000000000")]
    [InlineData("workflow.other", null)]
    public async Task An_unknown_id_or_hash_is_SND_RECIPE_002(string id, string? hash)
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");

        var shown = await r.Show(RecipeId.From(id), hash ?? step.Hash.Display);

        Assert.Equal(WorkflowErrors.RecipeNotFound.Code, shown.Error.Code);
        Assert.Contains("sindri recipe list", shown.Error.Message, StringComparison.Ordinal);
    }
}
