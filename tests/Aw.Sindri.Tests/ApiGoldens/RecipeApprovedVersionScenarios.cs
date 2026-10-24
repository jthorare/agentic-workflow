using System.Net;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The golden of the recipes list when a recipe was approved at an older version than the one registered now: 'GET /workflow/recipes' serves the shipped
/// 'version' and 'hash' with the 'approvedVersion' and 'approvedHash' the recipe was last approved at (the app tells "approved" from "approved earlier"
/// by them), and leaves both out for a recipe never approved.
/// </summary>
internal static class RecipeApprovedVersionScenarios
{
    internal const string Slug = "workflow-recipes";

    internal const string Scenario = "approved-older";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } = [(Slug, Scenario)];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Registers the step at an older version (approved) and at a newer one (not), and the item (never approved); returns the list.</summary>
    public static async Task<string> Body(GoldenHost host, CancellationToken ct)
    {
        var services = host.App.Services;
        var store = services.GetRequiredService<IRecipeStore>();
        var approvals = services.GetRequiredService<IRecipeApprovals>();
        var older = Read("sindri/workflow.step.yaml");
        var registeredOlder = await Register(store, older, ct);
        var pins = PinRules.Pins(older, services.GetRequiredService<BlockCatalog>(), [], SliceHarness.Json);
        _ = await approvals.Approve(new RecipeApproval(older.Id, older.Version, registeredOlder.Hash, "human:cli", host.Now(), ApprovalVia.Desk, older.Base, pins), ct);
        _ = await Register(store, older with { Version = older.Version + 1 }, ct);
        _ = await Register(store, Read("sindri/workflow.item.yaml"), ct);

        var listed = await host.Http.Ask(Get("/sindri/workflow/recipes", Human(host.Token.Value)));
        Assert.True(listed.Status == HttpStatusCode.OK, listed.Body);
        return listed.Body;
    }

    private static Recipe Read(string path) =>
        Workflow.RecipeReaderTests.Reader.Read(ShippedRecipes.Read(path), Path.GetFileName(path)).Document!.Recipe;   // why: the shipped recipes bind (ShippedRecipesTests)

    private static async Task<RegisteredRecipe> Register(IRecipeStore store, Recipe recipe, CancellationToken ct)
    {
        var hash = RecipeHashing.Of(recipe, SliceHarness.Json);
        var text = Encoding.UTF8.GetString(AwJson.Serialize(recipe with { Hash = null }, SliceHarness.Json));
        return (await store.Add(new RegisteredRecipe(recipe.Id, recipe.Version, hash, text, recipe.Base, DateTimeOffset.UnixEpoch), ct)).Value.Recipe;
    }
}
