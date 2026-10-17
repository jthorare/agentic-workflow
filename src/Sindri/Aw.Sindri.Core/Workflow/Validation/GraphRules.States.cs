using System.Globalization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Recipes;
using Edges = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>The timeout rules (spec §3.4, §17.28.5) and the loop-cap rules (spec §3.7, A §17.23.13).</summary>
internal sealed partial class GraphRules
{
    /// <summary>
    /// RCP-TIME-001: an active or wait state without a timeout, reported where the state starts. RCP-TIME-002: 'then:
    /// retry' on a child wait, which would start a second child instead of re-arming the same wait. RCP-TIME-003: on a state whose Step runs on the block runtime (DT spec §8.2), 'then: retry' (a run that outlives its
    /// 'maxDuration' ends as step.failed, with no timer park to retry), and a timeout that isn't longer than that 'maxDuration' (the state's timer would race the Step's own bound).
    /// </summary>
    private static IEnumerable<RecipeFinding> Timeouts(Recipe recipe, BlockCatalog catalog, RecipeSource source)
    {
        foreach (var (name, state) in recipe.States.Where(s => s.Value.Kind != StateKind.Terminal))
        {
            if (state.Timeout is null)
            {
                var kind = state.Kind == StateKind.Active ? "active" : "wait";
                yield return source.Finding(
                    "RCP-TIME-001",
                    $"states.{name}.timeout",
                    $"{kind} state '{name}' has no timeout",
                    "add timeout: { after, then: park | escalate | retry }");
            }
            else if (state.Timeout.Then == TimeoutAction.Retry && state.WaitFor?.Child is not null)
            {
                yield return source.Finding(
                    "RCP-TIME-002",
                    $"states.{name}.timeout.then",
                    $"state '{name}' retries a child wait when it times out",
                    "use then: park or then: escalate, since a child can't be retried");
            }
            else if (state.Step is { } step && catalog.Find(step.Use) is { Runtime: BlockRuntime.Block } block)
            {
                foreach (var finding in BlockTimeouts(name, state.Timeout, step, block, source))
                {
                    yield return finding;
                }
            }
        }
    }

    // RCP-TIME-003 for a state whose Step runs on the block runtime.
    private static IEnumerable<RecipeFinding> BlockTimeouts(string name, TimeoutSpec timeout, StepUse step, BlockDescriptor block, RecipeSource source)
    {
        if (timeout.Then == TimeoutAction.Retry)
        {
            yield return source.Finding(
                "RCP-TIME-003",
                $"states.{name}.timeout.then",
                $"state '{name}' runs {step.Use.Value} on the block runtime, where 'then: retry' doesn't apply",
                "use then: park (a judge call that runs out ends as step.failed, which the route sends to its desk twin)");
        }

        var max = BlockLimits.MaxDuration(block);
        if (timeout.After.Span <= max)
        {
            yield return source.Finding(
                "RCP-TIME-003",
                $"states.{name}.timeout.after",
                string.Create(CultureInfo.InvariantCulture, $"state '{name}' times out after {timeout.After.Span.TotalMinutes:0.##} min but {step.Use.Value} may run for {max.TotalMinutes:0.##} min, so the state's timer and the Step's own bound would race"),
                string.Create(CultureInfo.InvariantCulture, $"make the state timeout longer than the Step's maxDuration ({max.TotalMinutes:0.##} min)"));
        }
    }

    /// <summary>
    /// RCP-LOOP-001: a state names a loop the recipe doesn't declare, a loop's cap is below 1, or a cycle has no looped
    /// state (<see cref="UncappedCycles"/>). RCP-LOOP-002: a
    /// state in a loop isn't on a cycle, so its counter can never pass round 1. RCP-LOOP-003: an empty noProgress.
    /// </summary>
    private static IEnumerable<RecipeFinding> Loops(Recipe recipe, Edges edges, RecipeSource source)
    {
        foreach (var (name, loop) in recipe.Loops)
        {
            if (loop.Cap < 1)
            {
                yield return source.Finding(
                    "RCP-LOOP-001",
                    $"loops.{name}.cap",
                    string.Create(CultureInfo.InvariantCulture, $"loop '{name}' has cap {loop.Cap}"),
                    "set cap to 1 or more");
            }

            if (loop.NoProgress.Count == 0)
            {
                yield return source.Finding(
                    "RCP-LOOP-003",
                    $"loops.{name}.noProgress",
                    $"loop '{name}' has an empty noProgress",
                    "name at least one of findings, emptyDiff and sameFailure, or leave noProgress out to use all three");
            }
        }

        foreach (var (name, state) in recipe.States)
        {
            var finding = LoopMembership(recipe, name, state, edges, source);
            if (finding is not null)
            {
                yield return finding;
            }
        }

        foreach (var finding in UncappedCycles(recipe, edges, source))
        {
            yield return finding;
        }
    }

    /// <summary>
    /// RCP-LOOP-001 (§17.28.5 "every loop has a cap"): a cycle of step and wait transitions that passes through no state
    /// with a loop, so nothing counts its rounds. A cycle closed by a command, or by the answer to an answer wait, is a
    /// human's choice each round, not a loop; a cycle with no way out is RCP-EXIT-001's. Reported once per cycle, at its
    /// first state in the file's order.
    /// </summary>
    private static IEnumerable<RecipeFinding> UncappedCycles(Recipe recipe, Edges edges, RecipeSource source)
    {
        var automatic = UncountedEdges(recipe);
        var terminals = recipe.States.Where(s => s.Value.Kind == StateKind.Terminal).Select(s => s.Key);
        var canEnd = Reached(terminals, Reverse(edges));
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in recipe.States.Keys.Where(n => automatic.ContainsKey(n) && canEnd.Contains(n) && !reported.Contains(n)))
        {
            var ahead = Reached([name], automatic);
            if (!ahead.Contains(name))
            {
                continue;
            }

            var cycle = recipe.States.Keys.Where(n => n == name || (ahead.Contains(n) && Reached([n], automatic).Contains(name))).ToList();
            reported.UnionWith(cycle);
            yield return source.Finding(
                "RCP-LOOP-001",
                $"states.{name}",
                $"the cycle through {string.Join(", ", cycle.Select(n => $"'{n}'"))} has no loop cap",
                "add loop: <name> to one of its states and declare the loop under loops with a cap");
        }
    }

    /// <summary>The step and wait transitions between non-terminal states that have no loop: the edges no loop counts.</summary>
    private static Edges UncountedEdges(Recipe recipe)
    {
        var edges = recipe.States
            .Where(static s => s.Value.Loop is null && s.Value.Kind != StateKind.Terminal)
            .ToDictionary(static s => s.Key, static _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var transition in recipe.Transitions.Where(t => !t.On.StartsWith("command.", StringComparison.Ordinal) && edges.ContainsKey(t.To)))
        {
            foreach (var from in transition.From.Where(f => edges.ContainsKey(f) && !AnsweredByHuman(recipe, transition, f)))
            {
                _ = edges[from].Add(transition.To);
            }
        }

        return edges;
    }

    /// <summary>A wait.satisfied out of an answer wait: a human answers each round, so it closes a cycle like a command does.</summary>
    private static bool AnsweredByHuman(Recipe recipe, RecipeTransition transition, string from) =>
        transition.On == "wait.satisfied" && recipe.States[from].WaitFor?.Answer is not null;

    private static RecipeFinding? LoopMembership(Recipe recipe, string name, RecipeState state, Edges edges, RecipeSource source)
    {
        if (state.Loop is not { } loop)
        {
            return null;
        }

        if (!recipe.Loops.ContainsKey(loop))
        {
            return source.Finding(
                "RCP-LOOP-001",
                $"states.{name}.loop",
                $"state '{name}' names loop '{loop}', which isn't declared",
                "declare it under loops, or name a declared loop");
        }

        return Reached([name], edges).Contains(name)
            ? null
            : source.Finding(
                "RCP-LOOP-002",
                $"states.{name}.loop",
                $"state '{name}' is in loop '{loop}' but no transition leads back into it, so the loop never counts a second round",
                "add the transition that leads back into it, or remove loop");
    }
}
