using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Available = System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<Aw.Sindri.Core.Workflow.Ids.FactKey>>;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-WIRE-001 (W2): every fact a block, a 'with:' value or a child input needs is there on every path into where it runs.</summary>
internal sealed partial class WiringRules
{
    /// <summary>A fact something needs, and where.</summary>
    /// <param name="Path">The node that needs it.</param>
    /// <param name="What">How the finding names the needer: "step 'step.checks' in state 'checks'".</param>
    /// <param name="Fact">The fact.</param>
    /// <param name="State">The state it is needed in, or the state a transition leaves.</param>
    /// <param name="Transition">The transition it runs on, whose 'produces' it may also read; null for a state's own needs.</param>
    private sealed record Need(string Path, string What, FactKey Fact, string State, RecipeTransition? Transition);

    private static IEnumerable<RecipeFinding> Upstream(Recipe recipe, BlockCatalog catalog, RecipeSource source)
    {
        var available = Reaching(recipe);
        return StateNeeds(recipe, catalog)
            .Concat(TransitionNeeds(recipe, catalog))
            .Where(n => available.TryGetValue(n.State, out var facts) && !facts.Contains(n.Fact) && !(n.Transition?.Produces.Contains(n.Fact) ?? false))
            .DistinctBy(n => (n.Path, n.Fact))
            .Select(n => source.Finding(
                "RCP-WIRE-001",
                n.Path,
                $"{n.What} needs '{n.Fact.Value}', but {Witness(recipe, available, n.State, n.Fact)}",
                n.Transition is null
                    ? $"produce it on every path into '{n.State}', or add it to the recipe's inputs"
                    : $"produce it on this transition or on every path into '{n.State}', or add it to the recipe's inputs"));
    }

    /// <summary>
    /// The facts recorded on every path into each state the start reaches (a must-analysis to its greatest fixpoint):
    /// the initial state has the recipe's inputs; any other state has what every transition into it from a reached
    /// state carries (that state's facts plus its 'produces'). A state the start doesn't reach has no entry.
    /// </summary>
    private static Available Reaching(Recipe recipe)
    {
        var available = new Available(StringComparer.Ordinal);
        if (!recipe.States.ContainsKey(recipe.Initial))
        {
            return available;
        }

        available[recipe.Initial] = [.. recipe.Inputs.Select(i => i.Key)];
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var transition in recipe.Transitions)
            {
                foreach (var from in transition.From)
                {
                    changed |= Carry(recipe, available, from, transition);
                }
            }
        }

        return available;
    }

    private static bool Carry(Recipe recipe, Available available, string from, RecipeTransition transition)
    {
        if (transition.To == recipe.Initial || !recipe.States.ContainsKey(transition.To) || !available.TryGetValue(from, out var facts))
        {
            return false;
        }

        HashSet<FactKey> carried = [.. facts, .. transition.Produces];
        if (!available.TryGetValue(transition.To, out var current))
        {
            available[transition.To] = carried;
            return true;
        }

        var before = current.Count;
        current.IntersectWith(carried);
        return current.Count != before;
    }

    /// <summary>Why <paramref name="fact"/> isn't there in <paramref name="state"/>: the start, or the first path into it that doesn't carry it.</summary>
    private static string Witness(Recipe recipe, Available available, string state, FactKey fact)
    {
        if (state == recipe.Initial)
        {
            return "the start doesn't supply it";
        }

        var path = recipe.Transitions
            .Select((t, i) => (Transition: t, Index: i))
            .Where(p => p.Transition.To == state && !p.Transition.Produces.Contains(fact))
            .SelectMany(p => p.Transition.From.Select(from => (p.Index, From: from)))
            .FirstOrDefault(p => available.TryGetValue(p.From, out var facts) && !facts.Contains(fact));
        return path.From is null ? "not every path into it produces it" : $"the path through transitions[{Inv(path.Index)}] from '{path.From}' doesn't produce it";
    }

    private static IEnumerable<Need> StateNeeds(Recipe recipe, BlockCatalog catalog) =>
        recipe.States.SelectMany(s => StepNeeds(s.Key, s.Value.Step, catalog).Concat(ChildNeeds(s.Key, s.Value.WaitFor?.Child)));

    private static IEnumerable<Need> StepNeeds(string state, StepUse? step, BlockCatalog catalog)
    {
        if (step is null)
        {
            return [];
        }

        var where = $"in state '{state}'";
        var inputs = step.Inputs.SelectMany((input, k) => BlockNeeds($"states.{state}.step.inputs[{Inv(k)}]", "input", input, catalog, where));
        return BlockNeeds($"states.{state}.step", "step", new BlockUse(step.Use, step.With), catalog, where)
            .Concat(inputs)
            .Select(need => need with { State = state });
    }

    private static IEnumerable<Need> ChildNeeds(string state, ChildWait? child) =>
        (child?.Inputs ?? new Dictionary<FactKey, WithValue>())
            .Where(i => i.Value.Fact is not null)
            .Select(i => new Need(
                $"states.{state}.waitFor.child.inputs.{i.Key.Value}", $"child input '{i.Key.Value}' in state '{state}'", i.Value.Fact.GetValueOrDefault(), state,
                Transition: null));

    private static IEnumerable<Need> TransitionNeeds(Recipe recipe, BlockCatalog catalog)
    {
        for (var i = 0; i < recipe.Transitions.Count; i++)
        {
            var transition = recipe.Transitions[i];
            var on = $"on transitions[{Inv(i)}]";
            var needs = transition.Gates
                .SelectMany((g, j) => BlockNeeds($"transitions[{Inv(i)}].gates[{Inv(j)}]", "gate", new BlockUse(g.Use, g.With), catalog, on))
                .Concat(transition.Effects.SelectMany((e, j) => BlockNeeds($"transitions[{Inv(i)}].effects[{Inv(j)}]", "effect", e, catalog, on)))
                .Concat(transition.Evidence.SelectMany((e, j) => EvidenceNeeds($"transitions[{Inv(i)}].evidence[{Inv(j)}]", e.Kind, catalog, on)))
                .ToList();
            foreach (var from in transition.From.Distinct(StringComparer.Ordinal))
            {
                foreach (var need in needs)
                {
                    yield return need with { State = from, Transition = transition };
                }
            }
        }
    }

    /// <summary>
    /// A block's declared inputs, at its 'use', and the facts its 'with:' values read. The state is filled in by the
    /// caller. A block of the wrong kind for its place (a Step named as a gate) is RCP-BLOCK-002's, so its inputs aren't read.
    /// </summary>
    private static IEnumerable<Need> BlockNeeds(string path, string word, BlockUse use, BlockCatalog catalog, string where)
    {
        var what = $"{word} '{use.Use.Value}' {where}";
        var inputs = catalog.Find(use.Use) is { } descriptor && descriptor.Kind == KindOf(word) ? descriptor.Inputs : [];
        var declared = inputs.Select(fact => new Need($"{path}.use", what, fact, State: string.Empty, Transition: null));
        var read = use.With
            .Where(w => w.Value.Fact is not null)
            .Select(w => new Need($"{path}.with.{w.Key}", $"parameter '{w.Key}' of {what}", w.Value.Fact.GetValueOrDefault(), State: string.Empty, Transition: null));
        return declared.Concat(read);
    }

    private static BlockKind KindOf(string word) => word switch
    {
        "step" => BlockKind.Step,
        "input" => BlockKind.StepInput,
        "gate" => BlockKind.Gate,
        _ => BlockKind.Effect,
    };

    private static IEnumerable<Need> EvidenceNeeds(string path, string kind, BlockCatalog catalog, string where) =>
        BlockId.TryFrom($"evidence.{kind}", out var id) && catalog.Find(id) is { Kind: BlockKind.EvidenceProvider } provider
            ? provider.Inputs.Select(fact => new Need($"{path}.kind", $"evidence '{kind}' {where}", fact, State: string.Empty, Transition: null))
            : [];
}
