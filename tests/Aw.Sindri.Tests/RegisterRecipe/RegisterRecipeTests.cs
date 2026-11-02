using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Core.Workflow.Validation;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApproveRecipe;
using Aw.Sindri.Tests.Workflow;

namespace Aw.Sindri.Tests.RegisterRecipe;

/// <summary>
/// Registering a recipe (engine spec §4.1, §6.3 'recipe register', §8, §9.1): validated with the registry view, stored by
/// the hash of its normalized long form, 'recipe.registered' recorded once per hash; a failing recipe records nothing.
/// </summary>
public sealed class RegisterRecipeTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Registering_workflow_step_stores_it_by_hash_and_records_recipe_registered()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var expected = Read("sindri/workflow.step.yaml");

        var registered = await r.RegisterShipped("sindri/workflow.step.yaml");

        Assert.True(registered.IsSuccess, registered.IsError ? registered.Error.ToString() : null);
        var response = registered.Value;
        Assert.Equal((RecipeId.From("workflow.step"), 5, expected.Hash, true, RecipeStanding.Unapproved),
            (response.Id, response.Version, response.Hash, response.Added, response.Standing));
        var stored = (await r.Recipes.Find(expected.Hash, Token)).Value;
        Assert.NotNull(stored);
        Assert.Equal(expected.Hash, RecipeHashing.Of(RegistryHarness.Parse(stored), SliceHarness.Json));
        var recorded = Assert.Single(r.Events(EventTypes.RecipeRegistered));
        Assert.Equal(recorded.Sequence, response.Accepted.Sequence);
        Assert.Equal((expected.Recipe.Id, 5, expected.Hash), Payload(r, recorded));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Registering_the_same_hash_again_adds_nothing_and_records_no_second_event()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();

        var first = (await r.RegisterShipped("sindri/workflow.step.yaml")).Value;
        var again = await r.RegisterShipped("sindri/workflow.step.yaml");

        Assert.True(again.IsSuccess, again.IsError ? again.Error.ToString() : null);
        Assert.False(again.Value.Added);
        Assert.Equal((first.Hash, first.Accepted.Sequence), (again.Value.Hash, again.Value.Accepted.Sequence));
        _ = Assert.Single(r.Events(EventTypes.RecipeRegistered));
        _ = Assert.Single((await r.Recipes.All(Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Registering_an_approved_recipe_again_reports_it_approved()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        var step = await r.Shipped("sindri/workflow.step.yaml");
        _ = await r.Approve(step);

        var again = (await r.RegisterShipped("sindri/workflow.step.yaml")).Value;

        Assert.Equal((false, RecipeStanding.Approved), (again.Added, again.Standing));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_recipe_that_fails_validation_is_SND_RECIPE_001_with_its_rule_lines_and_records_nothing()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();

        var refused = await r.RegisterText(RecipeFixtures.Read("invalid/RCP-REACH-001.yaml"), "RCP-REACH-001.yaml");

        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, refused.Error.Code);
        Assert.Contains("RCP-REACH-001: ", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains(" at RCP-REACH-001.yaml:", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty((await r.Recipes.All(Token)).Value);
        Assert.Empty(r.Events(EventTypes.RecipeRegistered));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Same_id_and_version_with_other_content_is_SND_RECIPE_007_naming_RCP_VERSION_001()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        _ = await r.RegisterShipped("sindri/workflow.step.yaml");
        var edited = Edited();

        var refused = await r.RegisterText(edited, "workflow.step.yaml");

        Assert.Equal(WorkflowErrors.RecipeVersionTaken.Code, refused.Error.Code);
        Assert.Contains("RCP-VERSION-001: workflow.step version 5 is registered with hash ", refused.Error.Message, StringComparison.Ordinal);
        Assert.Contains("raise version to 6", refused.Error.Message, StringComparison.Ordinal);
        _ = Assert.Single((await r.Recipes.All(Token)).Value);
        _ = Assert.Single(r.Events(EventTypes.RecipeRegistered));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_version_below_the_highest_registered_is_RCP_VERSION_002()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();
        _ = await r.RegisterText(Edited().Replace("version: 5\n", "version: 7\n", StringComparison.Ordinal), "workflow.step.yaml");

        var refused = await r.RegisterText(Edited().Replace("version: 5\n", "version: 6\n", StringComparison.Ordinal), "workflow.step.yaml");

        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, refused.Error.Code);
        Assert.Contains("RCP-VERSION-002: workflow.step version 6 isn't registered and is below version 7", refused.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_parent_resolves_its_child_from_the_registered_recipes()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();

        var early = await r.RegisterShipped("sindri/workflow.item.yaml");
        _ = await r.RegisterShipped("sindri/workflow.step.yaml");
        var item = await r.RegisterShipped("sindri/workflow.item.yaml");

        Assert.Equal(WorkflowErrors.RecipeInvalid.Code, early.Error.Code);
        Assert.Contains("RCP-CHILD-001: ", early.Error.Message, StringComparison.Ordinal);
        Assert.True(item.IsSuccess, item.IsError ? item.Error.ToString() : null);
        Assert.Equal(Read("sindri/workflow.item.yaml").Hash, item.Value.Hash);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_service_cannot_register()
    {
        await using var r = new RegistryHarness();
        _ = await r.H.HoldLease();

        var refused = await r.RegisterText(ShippedRecipes.Read("sindri/workflow.step.yaml"), "workflow.step.yaml", A.Caller().Service("heartbeat").Build());

        Assert.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Assert.Empty((await r.Recipes.All(Token)).Value);
    }

    /// <summary>workflow.step with its first state's 'next' text changed: the same id and version, other content.</summary>
    private static string Edited()
    {
        var text = ShippedRecipes.Read("sindri/workflow.step.yaml");
        var at = text.IndexOf("next: \"", StringComparison.Ordinal) + "next: \"".Length;
        return text[..at] + "Edited. " + text[at..];
    }

    private static RecipeDocument Read(string path) =>
        RecipeReaderTests.Reader.Read(ShippedRecipes.Read(path), Path.GetFileName(path)).Document!;   // why: the shipped recipes bind (ShippedRecipesTests)

    private static (RecipeId, int, RecipeHash) Payload(RegistryHarness r, Core.Ledger.LedgerEvent recorded)
    {
        var payload = r.H.Events.Payload<RecipeRegistered>(recorded).Value;
        return (payload.Id, payload.Version, payload.Hash);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
