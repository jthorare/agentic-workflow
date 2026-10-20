using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-HUMAN-001 (PB spec W23): an effect whose descriptor says <c lang="csharp">HumanTrigger</c> acts with a human's authority, so it
/// may sit only on a transition a human drives: (a) a 'command.&lt;verb&gt;' transition, (b) a 'step.completed' out of
/// human-only Steps, or (c) a transition whose 'when' requires a fact present that only a state entered by (a)
/// transitions carrying a human-trigger effect produces, and that is no recipe input (the click's authority, carried). Pure.
/// </summary>
internal sealed class HumanTriggerRules : IRecipeRules
{
    private const string CommandPrefix = "command.";

    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        return Findings(document, context.Catalog);
    }

    private static IEnumerable<RecipeFinding> Findings(RecipeDocument document, BlockCatalog catalog)
    {
        var recipe = document.Recipe;
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            if (HumanDriven(transition, recipe, catalog))
            {
                continue;
            }

            for (var j = 0; j < transition.Effects.Count; j++)
            {
                if (!IsHumanTrigger(transition.Effects[j], catalog))
                {
                    continue;
                }

                yield return document.Source.Finding(
                    "RCP-HUMAN-001",
                    $"transitions[{Inv(i)}].effects[{Inv(j)}].use",
                    $"effect '{transition.Effects[j].Use.Value}' acts with a human's authority, but transitions[{Inv(i)}] ({string.Join(", ", transition.From)} -> {transition.To} on {transition.On}) isn't a human's act",
                    "put it on a command.<verb> transition, a step.completed out of a human-only Step, or a transition that requires a fact only such a transition produces");
            }
        }
    }

    private static bool IsHumanTrigger(BlockUse effect, BlockCatalog catalog) => catalog.Find(effect.Use) is { Kind: BlockKind.Effect, HumanTrigger: true };

    private static bool HumanDriven(RecipeTransition transition, Recipe recipe, BlockCatalog catalog) =>
        transition.On.StartsWith(CommandPrefix, StringComparison.Ordinal)
        || (string.Equals(transition.On, "step.completed", StringComparison.Ordinal) && transition.From.Count > 0 && transition.From.All(s => HumanOnly(recipe, s, catalog)))
        || (transition.When is { } when && RequiredPresent(when).Any(fact => Carried(fact, recipe, catalog)));

    private static bool HumanOnly(Recipe recipe, string state, BlockCatalog catalog) =>
        recipe.States.TryGetValue(state, out var s) && s.Step is { } step && catalog.Find(step.Use) is { HumanOnly: true };

    /// <summary>The facts a 'when' requires present: its own, or any inside an 'all'.</summary>
    private static IEnumerable<FactKey> RequiredPresent(WhenClause when)
    {
        if (when is { Fact: { } fact, Is: FactPresence.Present })
        {
            yield return fact;
        }

        foreach (var inner in when.All ?? [])
        {
            foreach (var nested in RequiredPresent(inner))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// True when <paramref name="fact"/> is not a recipe input and every state it can appear from is one only a human's command
    /// enters: the states a transition recording it leaves (its 'produces', or an effect or gate that writes it) and the
    /// states whose Step writes it. A fact with no such state is never carried.
    /// </summary>
    private static bool Carried(FactKey fact, Recipe recipe, BlockCatalog catalog)
    {
        if (recipe.Inputs.Any(i => i.Key == fact))
        {
            return false;
        }

        var sources = recipe.Transitions.Where(t => Records(t, fact, catalog)).SelectMany(t => t.From)
            .Concat(recipe.States.Where(s => s.Value.Step is { } step && Writes(step.Use, fact, catalog)).Select(s => s.Key))
            .ToList();
        return sources.Count > 0 && sources.TrueForAll(s => EnteredOnlyByHumanCommands(s, recipe, catalog));
    }

    private static bool Records(RecipeTransition transition, FactKey fact, BlockCatalog catalog) =>
        transition.Produces.Any(p => p.Fact == fact)
        || transition.Effects.Any(e => Writes(e.Use, fact, catalog))
        || transition.Gates.Any(g => Writes(g.Use, fact, catalog));

    private static bool Writes(BlockId block, FactKey fact, BlockCatalog catalog) => catalog.Find(block)?.Outputs.Any(o => o.Fact == fact) == true;

    private static bool EnteredOnlyByHumanCommands(string state, Recipe recipe, BlockCatalog catalog)
    {
        if (string.Equals(state, recipe.Initial, StringComparison.Ordinal))
        {
            return false;
        }

        var entering = recipe.Transitions.Where(t => string.Equals(t.To, state, StringComparison.Ordinal)).ToList();
        return entering.Count > 0
            && entering.TrueForAll(t => t.On.StartsWith(CommandPrefix, StringComparison.Ordinal) && t.Effects.Any(e => IsHumanTrigger(e, catalog)));
    }

    private static string Inv(int value) => value.ToString(CultureInfo.InvariantCulture);
}
