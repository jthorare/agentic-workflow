using System.Globalization;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The schema rules that need the bound long form (spec §3.2–§3.4, §9.1 "Schema", "Hash"): RCP-SCHEMA-001/002 for what
/// typed binding can't see (the per-kind fields of a state, exactly one form of a waitFor, a when or a with value, the
/// schema version, a token count, a transition's 'from' or 'to' naming an undeclared state), RCP-STATE-001, RCP-NAME-001, RCP-INIT-001, RCP-DUR-001 and RCP-HASH-001.
/// The reader reports RCP-SCHEMA-001–003 and RCP-DUR-001 on the file as written, before binding.
/// </summary>
internal sealed class SchemaRules : IRecipeRules
{
    /// <summary>The engine-owned state names a recipe can't use (spec §3.2).</summary>
    internal static IReadOnlySet<string> ReservedStates { get; } =
        new HashSet<string>(["cancelled", "escalated", "parked", "taken-over"], StringComparer.Ordinal);

    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context) =>
        [.. Structure(document.Recipe, document.Source), .. Hash(document)];

    /// <summary>Every schema rule except RCP-HASH-001, which needs the recipe as written in full.</summary>
    internal static IEnumerable<RecipeFinding> Structure(Recipe recipe, RecipeSource source) =>
    [
        .. Top(recipe, source),
        .. StateNames(recipe, source),
        .. NameRules.Check(recipe, source),
        .. Initial(recipe, source),
        .. TransitionStates(recipe, source),
        .. StateFieldRules.Check(recipe, source),
        .. ValueShapeRules.Check(recipe, source),
    ];

    private static IEnumerable<RecipeFinding> Top(Recipe recipe, RecipeSource source)
    {
        if (recipe.Schema != 1)
        {
            yield return source.Finding("RCP-SCHEMA-002", "schema",
                string.Create(CultureInfo.InvariantCulture, $"schema is {recipe.Schema}, but this build reads schema 1"), "set schema: 1");
        }

        if (recipe.Version < 1)
        {
            yield return source.Finding("RCP-SCHEMA-002", "version",
                string.Create(CultureInfo.InvariantCulture, $"version is {recipe.Version}"), "set version to a whole number of at least 1");
        }

        if (recipe.Budget is { Tokens: < 1 } budget)
        {
            yield return StateFieldRules.TooFewTokens(source, "budget.tokens", budget.Tokens);
        }
    }

    private static IEnumerable<RecipeFinding> StateNames(Recipe recipe, RecipeSource source)
    {
        foreach (var name in recipe.States.Keys.Where(ReservedStates.Contains))
        {
            yield return source.Finding("RCP-STATE-001", "states." + name, $"state name '{name}' is reserved for the engine",
                "rename it; cancelled, escalated, parked and taken-over are engine-owned states");
        }

        if (!recipe.States.Values.Any(static s => s.Kind is StateKind.Active or StateKind.Wait))
        {
            yield return source.Finding("RCP-STATE-001", "states", "the recipe has no active or wait state",
                "add a state with kind: active or kind: wait");
        }

        if (!recipe.States.Values.Any(static s => s.Kind == StateKind.Terminal))
        {
            yield return source.Finding("RCP-STATE-001", "states", "the recipe has no terminal state",
                "add a state with kind: terminal and an outcome");
        }
    }

    private static IEnumerable<RecipeFinding> Initial(Recipe recipe, RecipeSource source)
    {
        const string Fix = "set initial to an active or wait state";
        if (!recipe.States.TryGetValue(recipe.Initial, out var state))
        {
            yield return source.Finding("RCP-INIT-001", "initial", $"initial state '{recipe.Initial}' isn't one of the recipe's states", Fix);
        }
        else if (state.Kind == StateKind.Terminal)
        {
            yield return source.Finding("RCP-INIT-001", "initial", $"initial state '{recipe.Initial}' is a terminal state", Fix);
        }
    }

    /// <summary>RCP-SCHEMA-002: a transition's 'from' entry or 'to' that names no state of the recipe (spec §3.5).</summary>
    private static IEnumerable<RecipeFinding> TransitionStates(Recipe recipe, RecipeSource source)
    {
        var fix = recipe.States.Count == 0 ? "declare the state under states" : "name one of the recipe's states: " + string.Join(", ", recipe.States.Keys);
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            for (var j = 0; j < transition.From.Count; j++)
            {
                if (!recipe.States.ContainsKey(transition.From[j]))
                {
                    yield return source.Finding("RCP-SCHEMA-002", string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].from[{j}]"),
                        $"transition from '{transition.From[j]}' names a state the recipe doesn't declare", fix);
                }
            }

            if (!recipe.States.ContainsKey(transition.To))
            {
                yield return source.Finding("RCP-SCHEMA-002", string.Create(CultureInfo.InvariantCulture, $"transitions[{i}].to"),
                    $"transition to '{transition.To}' names a state the recipe doesn't declare", fix);
            }
        }
    }

    private static IEnumerable<RecipeFinding> Hash(RecipeDocument document)
    {
        if (document.Recipe.Hash is { } stated && !stated.Equals(document.Hash))
        {
            yield return document.Source.Finding("RCP-HASH-001", "hash",
                $"hash {stated.Display} doesn't match the recipe's content, whose hash is {document.Hash.Display}",
                $"set hash to {document.Hash.Value}, or remove it");
        }
    }
}
