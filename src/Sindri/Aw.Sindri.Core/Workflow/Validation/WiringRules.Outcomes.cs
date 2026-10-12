using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// RCP-WIRE-002 ('produces' from the trigger's source), RCP-WHEN-001/002 (a 'when' fits its trigger and the recipe's
/// loops), RCP-OUTCOME-001 (every outcome a source reports is handled, and terminals use W6's closed set) and
/// RCP-CHILD-001 (the child exists and gets its inputs).
/// </summary>
internal sealed partial class WiringRules
{
    private static readonly HashSet<OutcomeName> _recipeOutcomes =
        [OutcomeName.From("resolved"), OutcomeName.From("unresolved"), OutcomeName.From("alreadySatisfied"), OutcomeName.From("failed")];

    private static IEnumerable<RecipeFinding> Produced(Recipe recipe, ValidationContext context, RecipeSource source) =>
        recipe.Transitions
            .SelectMany((t, i) => t.Produces.SelectMany((fact, j) => Sources(recipe, t, context)
                .Where(s => !s.Source.Facts.Contains(fact))
                .Take(1)
                .Select(s => source.Finding(
                    "RCP-WIRE-002",
                    $"transitions[{Inv(i)}].produces[{Inv(j)}]",
                    $"transition from '{s.From}' produces '{fact.Value}', which {s.Source.Name} doesn't record",
                    s.Source.Facts.Count == 0
                        ? $"remove it, since {s.Source.Name} records no facts"
                        : $"list only what it records ({List(s.Source.Facts)}), or remove it"))));

    private static IEnumerable<RecipeFinding> Conditions(Recipe recipe, ValidationContext context, RecipeSource source) =>
        recipe.Transitions
            .SelectMany((t, i) => Clauses(t.When, $"transitions[{Inv(i)}].when")
                .SelectMany(c => ClauseFindings(recipe, t, c, context, source)))
            .DistinctBy(f => (f.Path, f.Rule));

    private static IEnumerable<(string Path, WhenClause Clause)> Clauses(WhenClause? when, string path)
    {
        if (when is null)
        {
            return [];
        }

        var all = (when.All ?? []).SelectMany((c, k) => Clauses(c, $"{path}.all[{Inv(k)}]"));
        var any = (when.Any ?? []).SelectMany((c, k) => Clauses(c, $"{path}.any[{Inv(k)}]"));
        return [(path, when), .. all, .. any, .. Clauses(when.Not, $"{path}.not")];
    }

    private static IEnumerable<RecipeFinding> ClauseFindings(
        Recipe recipe,
        RecipeTransition transition,
        (string Path, WhenClause Clause) node,
        ValidationContext context,
        RecipeSource source)
    {
        var (path, clause) = node;
        if (clause.Loop is { } loop && !recipe.Loops.ContainsKey(loop))
        {
            var from = string.Join(", ", transition.From.Select(f => $"'{f}'"));
            yield return source.Finding(
                "RCP-WHEN-002", $"{path}.loop", $"transition from {from} tests loop '{loop}', which the recipe doesn't declare",
                "declare it under loops, or test a declared loop");
        }

        if (clause.Outcome is not { } outcome)
        {
            yield break;
        }

        foreach (var (from, trigger) in Sources(recipe, transition, context).Where(s => !s.Source.Outcomes.Contains(outcome)))
        {
            var tests = $"transition from '{from}' tests outcome '{outcome.Value}'";
            yield return trigger.Outcomes.Count == 0
                ? source.Finding("RCP-WHEN-002", $"{path}.outcome", $"{tests}, but {trigger.Name} reports no outcome",
                    "remove the outcome test, since this trigger has none")
                : source.Finding("RCP-WHEN-001", $"{path}.outcome", $"{tests}, which {trigger.Name} doesn't report",
                    $"test one of its outcomes: {List(trigger.Outcomes)}");
        }
    }

    private static IEnumerable<RecipeFinding> Unhandled(Recipe recipe, ValidationContext context, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States)
        {
            var on = state.Kind switch { StateKind.Active => "step.completed", StateKind.Wait => "wait.satisfied", StateKind.Terminal => null, _ => null };
            if (on is null || HasMisfit(recipe, name, state.Kind) || SourceOf(recipe, name, on, context) is not { Outcomes.Count: > 0 } trigger)
            {
                continue;
            }

            var handled = Handled(recipe, name, on);
            foreach (var outcome in trigger.Outcomes.Where(o => handled is not null && !handled.Contains(o)))
            {
                yield return source.Finding(
                    "RCP-OUTCOME-001", $"states.{name}", $"state '{name}' has no transition for outcome '{outcome.Value}' of {trigger.Name}",
                    $"add a transition from '{name}' on {on} with when: {{ outcome: {outcome.Value} }}");
            }
        }
    }

    /// <summary>
    /// Whether a transition from <paramref name="state"/> has a trigger the state can't fire (a wait trigger on an active
    /// state, an unknown trigger). That is RCP-TRIG-001's one finding: the transition was meant to handle the state's
    /// outcomes, so they aren't reported as unhandled until its trigger is fixed.
    /// </summary>
    private static bool HasMisfit(Recipe recipe, string state, StateKind kind) =>
        recipe.Transitions.Any(t =>
            t.From.Contains(state, StringComparer.Ordinal)
            && !t.On.StartsWith("command.", StringComparison.Ordinal)
            && (kind, t.On) is not ((StateKind.Active, "step.completed" or "step.failed") or (StateKind.Wait, "wait.satisfied")));

    /// <summary>The outcomes the transitions from <paramref name="state"/> on <paramref name="on"/> can match, or null when one matches any outcome.</summary>
    private static HashSet<OutcomeName>? Handled(Recipe recipe, string state, string on)
    {
        HashSet<OutcomeName> handled = [];
        foreach (var transition in recipe.Transitions.Where(t => t.On == on && t.From.Contains(state, StringComparer.Ordinal)))
        {
            if (Matches(transition.When) is not { } outcomes)
            {
                return null;
            }

            handled.UnionWith(outcomes);
        }

        return handled;
    }

    /// <summary>
    /// The outcomes a condition can hold for, or null for any outcome. Only the outcome tests narrow it: a fact or
    /// loop test (and a 'not') leaves every outcome possible, 'all' narrows to what each part allows, and 'any' widens.
    /// </summary>
    private static HashSet<OutcomeName>? Matches(WhenClause? when) => when switch
    {
        { Outcome: { } outcome } => [outcome],
        { All: { Count: > 0 } all } => all.Select(Matches).Aggregate((HashSet<OutcomeName>?)null, (acc, next) =>
            next is null ? acc : acc is null ? [.. next] : [.. acc.Intersect(next)]),
        { Any: { } any } => any.Select(Matches).Aggregate((HashSet<OutcomeName>?)[], (acc, next) =>
            acc is null || next is null ? null : [.. acc.Union(next)]),
        _ => null,
    };

    private static IEnumerable<RecipeFinding> Terminals(Recipe recipe, RecipeSource source) =>
        recipe.States
            .Where(s => s.Value.Kind == StateKind.Terminal && s.Value.Outcome is { } o && !_recipeOutcomes.Contains(o))
            .Select(s => source.Finding(
                "RCP-OUTCOME-001",
                $"states.{s.Key}.outcome",
                $"terminal state '{s.Key}' ends with outcome '{s.Value.Outcome.GetValueOrDefault().Value}', which isn't a recipe outcome",
                "use resolved, unresolved, alreadySatisfied or failed"));

    private static IEnumerable<RecipeFinding> Children(Recipe recipe, IChildRecipeResolver children, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States)
        {
            if (state.WaitFor?.Child is not { } child)
            {
                continue;
            }

            var id = child.Recipe.Value;
            if (child.Recipe.Equals(recipe.Id))
            {
                // why: a recipe that starts itself is RCP-CHILD-002's cycle, not a missing child
                continue;
            }

            if (children.Resolve(child.Recipe) is not { } found)
            {
                yield return source.Finding(
                    "RCP-CHILD-001", $"states.{name}.waitFor.child.recipe", $"child recipe '{id}' isn't registered or next to this recipe",
                    $"register it, or put {id}.yaml in this recipe's directory");
                continue;
            }

            foreach (var key in found.Recipe.Inputs.Select(i => i.Key).Where(k => !child.Inputs.ContainsKey(k)))
            {
                yield return source.Finding(
                    "RCP-CHILD-001", $"states.{name}.waitFor.child.inputs", $"child '{id}' needs input '{key.Value}', which state '{name}' doesn't give it",
                    $"add \"{key.Value}\": {{ fact: <a fact> }} or a literal to its inputs");
            }
        }
    }
}
