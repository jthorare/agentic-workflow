using System.Globalization;
using System.Text.Json;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>The block uses and children a recipe names, located by path, and RCP-CAP-002.</summary>
internal sealed partial class CapabilityRules
{
    private static readonly Dictionary<string, WithValue> _noArguments = new(StringComparer.Ordinal);

    /// <summary>
    /// Every block use and child of <paramref name="recipe"/>, in file order (states, then transitions). A transition
    /// use is located by from -> to, once per 'from' state; a Step, a Step input or a child by the state it's in.
    /// </summary>
    private static IEnumerable<CapabilityUse> UsesOf(Recipe recipe) =>
        recipe.States.SelectMany(s => StateUses(s.Key, s.Value))
            .Concat(recipe.Transitions.SelectMany(TransitionUses));

    private static IEnumerable<CapabilityUse> StateUses(string name, RecipeState state)
    {
        var where = $"in state '{name}'";
        if (state.Step is { } step)
        {
            yield return new CapabilityUse(step.Use.Value, where, $"states.{name}.step", "use", step.Use, step.With, name, Transition: null);
            for (var k = 0; k < step.Inputs.Count; k++)
            {
                var input = step.Inputs[k];
                yield return new CapabilityUse(input.Use.Value, where, string.Create(CultureInfo.InvariantCulture, $"states.{name}.step.inputs[{k}]"), "use", input.Use, input.With, name, Transition: null);
            }
        }

        if (state.WaitFor?.Child is { } child)
        {
            yield return new CapabilityUse($"child {child.Recipe.Value}", where, $"states.{name}.waitFor.child", "recipe", Block: null, _noArguments, name, Transition: null);
        }
    }

    private static IEnumerable<CapabilityUse> TransitionUses(RecipeTransition transition, int index)
    {
        foreach (var from in transition.From)
        {
            var where = $"on {from} -> {transition.To}";
            for (var j = 0; j < transition.Gates.Count; j++)
            {
                var gate = transition.Gates[j];
                yield return new CapabilityUse(gate.Use.Value, where, string.Create(CultureInfo.InvariantCulture, $"transitions[{index}].gates[{j}]"), "use", gate.Use, gate.With, from, transition);
            }

            for (var j = 0; j < transition.Effects.Count; j++)
            {
                var effect = transition.Effects[j];
                yield return new CapabilityUse(effect.Use.Value, where, string.Create(CultureInfo.InvariantCulture, $"transitions[{index}].effects[{j}]"), "use", effect.Use, effect.With, from, transition);
            }
        }
    }

    /// <summary>
    /// RCP-CAP-002: each capability parameter of a kept use equals the base's. It passes when any base use at the same
    /// place matches; otherwise each differing parameter is reported against the first.
    /// </summary>
    private static IEnumerable<RecipeFinding> Parameters(Comparison c, CapabilityUse use, IReadOnlyList<CapabilityUse> candidates)
    {
        if (use.Block is not { } block || c.Catalog.Find(block) is not { } descriptor)
        {
            return [];
        }

        var names = descriptor.Parameters.Where(p => p.IsCapabilityField).Select(p => p.Name).ToList();
        if (candidates.Any(b => names.TrueForAll(n => SameValue(use.Argument(n), b.Argument(n)))))
        {
            return [];
        }

        var first = candidates[0];
        return names
            .Where(n => !SameValue(use.Argument(n), first.Argument(n)))
            .Select(n => c.Source.Finding(
                "RCP-CAP-002",
                use.ArgumentPath(n),
                $"capability parameter '{n}' of {use.Label} {use.Where} is {Show(use.Argument(n))}, but base {c.Base.Id.Value} has {Show(first.Argument(n))}",
                "set it to the base's value"));
    }

    /// <summary>Whether two arguments are the same: both unset, equal literals (as JSON), or the same fact or profile key.</summary>
    private static bool SameValue(WithValue? a, WithValue? b) => (a, b) switch
    {
        (null, null) => true,
        (null, _) or (_, null) => false,
        ({ Literal: { } x }, { Literal: { } y }) => JsonElement.DeepEquals(x, y),
        ({ Literal: null }, { Literal: null }) => a.Fact == b.Fact && string.Equals(a.Profile, b.Profile, StringComparison.Ordinal),
        _ => false,
    };

    private static bool SameArguments(IReadOnlyDictionary<string, WithValue> a, IReadOnlyDictionary<string, WithValue> b) =>
        a.Count == b.Count && a.All(p => SameValue(p.Value, b.GetValueOrDefault(p.Key)));

    private static string Show(WithValue? value) => value switch
    {
        null => "unset",
        { Literal: { } literal } => literal.GetRawText(),
        { Fact: { } fact } => $"{{ fact: {fact.Value} }}",
        _ => $"{{ profile: {value.Profile} }}",
    };

    /// <summary>A block use or a child, where the recipe names it.</summary>
    /// <param name="Label">The block id, or 'child' and the child's recipe id.</param>
    /// <param name="Where">'on from -> to' for a transition, "in state 'name'" for a Step, Step input or child.</param>
    /// <param name="Node">The node's path, for example 'transitions[2].effects[0]'.</param>
    /// <param name="Leaf">The field a finding about the use points at: 'use', or 'recipe' for a child.</param>
    /// <param name="Block">The block; null for a child.</param>
    /// <param name="With">Its arguments.</param>
    /// <param name="State">The state whose dominating guards apply: the 'from' state, or the state it's in.</param>
    /// <param name="Transition">The transition whose own guards apply; null for a Step, Step input or child.</param>
    private sealed record CapabilityUse(
        string Label,
        string Where,
        string Node,
        string Leaf,
        BlockId? Block,
        IReadOnlyDictionary<string, WithValue> With,
        string State,
        RecipeTransition? Transition)
    {
        public string Path => $"{Node}.{Leaf}";

        public string ArgumentPath(string name) => $"{Node}.with.{name}";

        public WithValue? Argument(string name) => With.GetValueOrDefault(name);

        public bool SameUse(CapabilityUse other) =>
            string.Equals(Label, other.Label, StringComparison.Ordinal) && string.Equals(Where, other.Where, StringComparison.Ordinal);
    }
}
