using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// A recipe's hash (spec §3.1): 'sha256:' over the RFC 8785 canonical JSON of the normalized long form without 'hash'.
/// The long form is the bound record as the package's AwJson options write it, so a short form and its long form, or
/// '200k' and 200000, hash the same.
/// </summary>
public static class RecipeHashing
{
    /// <summary>The hash of <paramref name="recipe"/>.</summary>
    /// <param name="recipe">The long form.</param>
    /// <param name="options">The package's AwJson options (they know <see cref="Recipe"/>).</param>
    /// <returns>The hash; <see cref="Recipe.Hash"/> doesn't count.</returns>
    public static RecipeHash Of(Recipe recipe, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        // why: a serialized record is a JSON object, never the null literal
        var node = JsonNode.Parse(AwJson.Serialize(recipe with { Hash = null }, options))!.AsObject();
        _ = node.Remove("hash");
        return RecipeHash.Of(CanonicalJson.Of(node));
    }
}
