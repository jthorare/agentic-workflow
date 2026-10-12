using System.Globalization;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>RCP-CAP-005: the guards that dominate each kept capability use are a superset of the base's.</summary>
internal sealed partial class CapabilityRules
{
    private const string KeepGuards = "keep the base's gates and evidence on every path to it";

    /// <summary>
    /// For each kept use, the gates and evidence on every path from initial to it plus its own (spec §4.3) must cover
    /// the base's: each base gate by a gate with the same 'with:' and a threshold at or above the base's, and each
    /// base evidence entry by the same kind, 'at' and 'expect'. A use no path reaches is RCP-REACH-001's.
    /// </summary>
    private static IEnumerable<RecipeFinding> DominatingGuards(
        Comparison c,
        IReadOnlyList<(CapabilityUse Use, IReadOnlyList<CapabilityUse> Candidates)> kept)
    {
        var generated = Dominating(c.Generated);
        var based = Dominating(c.Base);
        foreach (var (use, candidates) in kept)
        {
            if (GuardsOf(use, generated) is not { } actual)
            {
                continue;
            }

            var lost = candidates.Select(b => Lost(GuardsOf(b, based) ?? OwnOrNone(b), actual, c.Base.Id)).ToList();
            if (lost.Exists(l => l.Count == 0))
            {
                continue;
            }

            foreach (var (what, fix) in lost[0])
            {
                yield return c.Source.Finding("RCP-CAP-005", use.Path, $"capability use {use.Label} {use.Where} {what}", fix);
            }
        }
    }

    /// <summary>What <paramref name="actual"/> lacks of <paramref name="required"/>, each with its fix.</summary>
    private static List<(string What, string Fix)> Lost(Guards required, Guards actual, RecipeId baseId)
    {
        var lost = new List<(string What, string Fix)>();
        foreach (var gate in required.Gates)
        {
            var match = actual.Gates.FirstOrDefault(g => g.SameGate(gate));
            if (match is null)
            {
                lost.Add(($"lost gate {gate.Use.Value}, which guards it in base {baseId.Value}", KeepGuards));
            }
            else if (gate.Threshold is { } floor && !(match.Threshold >= floor))
            {
                var shown = match.Threshold is { } t ? "at threshold " + Number(t) : "without a threshold";
                lost.Add((
                    $"is guarded by {gate.Use.Value} {shown}, below base {baseId.Value}'s {Number(floor)}",
                    $"raise the threshold to {Number(floor)} or more"));
            }
        }

        lost.AddRange(required.Evidence.Where(e => !actual.Evidence.Contains(e)).Select(e => (
            $"lost evidence {e.Kind} at {Point(e.At)} expecting {Expectation(e.Expect)}, which guards it in base {baseId.Value}",
            KeepGuards)));
        return lost;
    }

    /// <summary>The guards on every path from initial to each state; null for a state no path reaches.</summary>
    private static Dictionary<string, Guards?> Dominating(Recipe recipe)
    {
        var dominating = recipe.States.Keys.ToDictionary(s => s, _ => (Guards?)null, StringComparer.Ordinal);
        if (!dominating.ContainsKey(recipe.Initial))
        {
            return dominating;
        }

        dominating[recipe.Initial] = Guards.None;
        for (var changed = true; changed;)
        {
            changed = false;
            foreach (var state in recipe.States.Keys.Where(s => !string.Equals(s, recipe.Initial, StringComparison.Ordinal)))
            {
                if (Incoming(recipe, dominating, state) is { } meet && (dominating[state] is not { } current || !meet.SameAs(current)))
                {
                    dominating[state] = meet;
                    changed = true;
                }
            }
        }

        return dominating;
    }

    /// <summary>What holds on every way into <paramref name="state"/> so far: the meet over its reached incoming transitions.</summary>
    private static Guards? Incoming(Recipe recipe, Dictionary<string, Guards?> dominating, string state)
    {
        Guards? meet = null;
        foreach (var transition in recipe.Transitions.Where(t => string.Equals(t.To, state, StringComparison.Ordinal)))
        {
            foreach (var from in transition.From)
            {
                if (dominating.GetValueOrDefault(from) is { } before)
                {
                    var via = before.Union(Guards.Of(transition));
                    meet = meet is null ? via : meet.Meet(via);
                }
            }
        }

        return meet;
    }

    private static Guards? GuardsOf(CapabilityUse use, Dictionary<string, Guards?> dominating) =>
        dominating.GetValueOrDefault(use.State) is { } before
            ? (use.Transition is { } own ? before.Union(Guards.Of(own)) : before)
            : null;

    private static Guards OwnOrNone(CapabilityUse use) => use.Transition is { } own ? Guards.Of(own) : Guards.None;

    private static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Point(EvidencePoint at) => at == EvidencePoint.Base ? "base" : "head";

    private static string Expectation(EvidenceExpectation expect) => expect == EvidenceExpectation.Pass ? "pass" : "fail";

    /// <summary>A gate as a guard: the same block with the same 'with:' is the same gate; a missing threshold is the weakest.</summary>
    private sealed record GateGuard(BlockId Use, IReadOnlyDictionary<string, WithValue> With, double? Threshold)
    {
        public bool SameGate(GateGuard other) => Use == other.Use && SameArguments(With, other.With);
    }
}
