using Aw.Sindri.Core.Workflow.Recipes;
using Edges = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// The graph rules (spec §9.1): RCP-REACH-001 (reachable), RCP-EXIT-001 (a way out), RCP-TIME-001/002/003 (timeouts), RCP-LOOP-001–003 (loop caps), RCP-TRIG-001/002 (triggers), RCP-NEXT-001/002 and RCP-CHILD-002/003 (child cycles and depth).
/// Pure: they read the bound recipe and the context's child resolver only. A state name the recipe doesn't declare
/// (an unknown initial, 'from' or 'to') is the schema rules' to report, so these rules skip it. The findings come
/// back ordered by where their node starts in the file, then by rule.
/// </summary>
internal sealed partial class GraphRules : IRecipeRules
{
    public IEnumerable<RecipeFinding> Check(RecipeDocument document, ValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);
        var recipe = document.Recipe;
        var source = document.Source;
        var edges = Successors(recipe);
        IEnumerable<RecipeFinding> findings =
        [
            .. Unreachable(recipe, edges, source),
            .. NoWayOut(recipe, edges, source),
            .. Timeouts(recipe, context.Catalog, source),
            .. Loops(recipe, edges, source),
            .. Triggers(recipe, source),
            .. NextTexts(recipe, source),
            .. ChildChains(recipe, context.Children, source),
        ];
        return
        [
            .. findings
                .OrderBy(f => f.Line)
                .ThenBy(f => f.Column)
                .ThenBy(f => f.Rule, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// RCP-REACH-001: a state no chain of transitions from 'initial' enters. A terminal initial state reaches nothing,
    /// which is RCP-INIT-001's one finding, so this rule waits until 'initial' is fixed.
    /// </summary>
    private static IEnumerable<RecipeFinding> Unreachable(Recipe recipe, Edges edges, RecipeSource source)
    {
        if (!recipe.States.TryGetValue(recipe.Initial, out var initial) || initial.Kind == StateKind.Terminal)
        {
            return [];
        }

        var reached = Reached([recipe.Initial], edges);
        _ = reached.Add(recipe.Initial);
        return recipe.States.Keys
            .Where(name => !reached.Contains(name))
            .Select(name => source.Finding(
                "RCP-REACH-001",
                $"states.{name}",
                $"state '{name}' is unreachable from '{recipe.Initial}'",
                "add a transition into it or remove it"));
    }

    /// <summary>RCP-EXIT-001: a non-terminal state from which no chain of transitions enters a terminal state (no transition out, or a trap cycle).</summary>
    private static IEnumerable<RecipeFinding> NoWayOut(Recipe recipe, Edges edges, RecipeSource source)
    {
        var terminals = recipe.States.Where(s => s.Value.Kind == StateKind.Terminal).Select(s => s.Key).ToList();
        var canEnd = Reached(terminals, Reverse(edges));
        return recipe.States
            .Where(s => s.Value.Kind != StateKind.Terminal && !canEnd.Contains(s.Key))
            .Select(s => source.Finding(
                "RCP-EXIT-001",
                $"states.{s.Key}",
                $"no transition from state '{s.Key}' leads to a terminal state",
                "add a transition from it, or from a state it leads to, into a terminal state"));
    }

    /// <summary>Each declared state's successors: the declared states a transition from it enters.</summary>
    private static Edges Successors(Recipe recipe)
    {
        var edges = recipe.States.Keys.ToDictionary(name => name, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var transition in recipe.Transitions.Where(t => recipe.States.ContainsKey(t.To)))
        {
            foreach (var from in transition.From)
            {
                if (edges.TryGetValue(from, out var targets))
                {
                    _ = targets.Add(transition.To);
                }
            }
        }

        return edges;
    }

    private static Edges Reverse(Edges edges)
    {
        var reverse = edges.Keys.ToDictionary(name => name, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var (from, targets) in edges)
        {
            foreach (var to in targets)
            {
                _ = reverse[to].Add(from);
            }
        }

        return reverse;
    }

    /// <summary>The states one or more edges away from <paramref name="starts"/>; a start is in it only when a cycle leads back to it.</summary>
    private static HashSet<string> Reached(IEnumerable<string> starts, Edges edges)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(starts);
        while (queue.TryDequeue(out var state))
        {
            foreach (var next in edges[state])
            {
                if (reached.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }
}
