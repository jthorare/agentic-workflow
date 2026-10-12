using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ValidateRecipe;

namespace Aw.Sindri.Host.Commands.Recipe;

/// <summary>
/// What 'recipe validate --json' prints: whether it passed, the hash, the findings in the plain output's order, and the
/// normalized long form the hash covers.
/// </summary>
/// <param name="Passed">Exit 0: the recipe bound and no finding is an error.</param>
/// <param name="Hash">The recipe's hash; null when it didn't bind.</param>
/// <param name="Findings">Errors, then information, then skipped.</param>
/// <param name="Normalized">The normalized long form without 'hash': its RFC 8785 canonical JSON hashes to <paramref name="Hash"/>. Null when it didn't bind.</param>
internal sealed record RecipeValidationView(bool Passed, RecipeHash? Hash, IReadOnlyList<RecipeFindingView> Findings, JsonObject? Normalized)
{
    /// <summary>The view of <paramref name="response"/>.</summary>
    /// <param name="response">The validation.</param>
    /// <param name="options">The package's AwJson options, the ones the hash was computed with (RecipeHashing.Of).</param>
    /// <returns>The view.</returns>
    internal static RecipeValidationView From(ValidateRecipeResponse response, JsonSerializerOptions options)
    {
        var findings = RecipeText.Ordered(response.Findings)
            .Select(static f => new RecipeFindingView(f.Rule, f.Severity, f.Message, f.File, f.Line, f.Column, f.Path, f.Fix))
            .ToList();
        return new RecipeValidationView(response.Passed, response.Hash, findings, response.Normalized is { } recipe ? LongForm(recipe, options) : null);
    }

    /// <summary>The long form as RecipeHashing.Of hashes it: the record through the package's options, without 'hash'.</summary>
    private static JsonObject LongForm(Core.Workflow.Recipes.Recipe recipe, JsonSerializerOptions options)
    {
        // why: a serialized record is a JSON object, never the null literal
        var node = JsonNode.Parse(AwJson.Serialize(recipe with { Hash = null }, options))!.AsObject();
        _ = node.Remove("hash");
        return node;
    }
}
