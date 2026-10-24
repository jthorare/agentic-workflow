using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The child-chain rules (spec §9.1): RCP-CHILD-002 (child recipes form a cycle) and RCP-CHILD-003 (they nest more
/// than <see cref="MaxChildDepth"/> deep). Children resolve through <see cref="IChildRecipeResolver"/>; one it can't
/// find ends that chain here and is RCP-CHILD-001's to report (the wiring rules).
/// </summary>
internal sealed partial class GraphRules
{
    /// <summary>How deep children may nest: the recipe's own children are depth 1.</summary>
    private const int MaxChildDepth = 10;

    /// <summary>For each child wait of the recipe, the first chain under it that cycles or nests too deep, reported at the wait's 'recipe'.</summary>
    private static IEnumerable<RecipeFinding> ChildChains(Recipe recipe, IChildRecipeResolver children, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States)
        {
            if (state.WaitFor?.Child is not { } child)
            {
                continue;
            }

            var chain = FirstBadChain([recipe.Id, child.Recipe], children);
            if (chain is not null)
            {
                yield return ChainFinding(chain, $"states.{name}.waitFor.child.recipe", source);
            }
        }
    }

    /// <summary>
    /// Follows <paramref name="chain"/> (the recipe, then each child) depth first, and returns the first chain whose
    /// last recipe already appears in it (a cycle) or that is more than <see cref="MaxChildDepth"/> children deep.
    /// </summary>
    private static List<RecipeId>? FirstBadChain(List<RecipeId> chain, IChildRecipeResolver children)
    {
        var last = chain[^1];
        if (chain.IndexOf(last) < chain.Count - 1 || chain.Count - 1 > MaxChildDepth)
        {
            return chain;
        }

        var resolved = children.Resolve(last);
        if (resolved is null)
        {
            return null;
        }

        foreach (var state in resolved.Recipe.States.Values)
        {
            if (state.WaitFor?.Child is { } child && FirstBadChain([.. chain, child.Recipe], children) is { } bad)
            {
                return bad;
            }
        }

        return null;
    }

    private static RecipeFinding ChainFinding(List<RecipeId> chain, string path, RecipeSource source)
    {
        var text = string.Join(" -> ", chain.Select(id => id.Value));
        return chain.IndexOf(chain[^1]) < chain.Count - 1
            ? source.Finding(
                "RCP-CHILD-002",
                path,
                $"child recipes form a cycle: {text}",
                "remove a child wait so no recipe starts itself, directly or through its children")
            : source.Finding(
                "RCP-CHILD-003",
                path,
                string.Create(CultureInfo.InvariantCulture, $"child recipes nest {chain.Count - 1} deep ({text}), more than {MaxChildDepth}"),
                string.Create(CultureInfo.InvariantCulture, $"flatten the chain so children nest at most {MaxChildDepth} deep"));
    }
}
