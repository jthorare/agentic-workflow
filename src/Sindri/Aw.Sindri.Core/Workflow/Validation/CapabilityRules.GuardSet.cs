using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>The guard sets RCP-CAP-005 compares.</summary>
internal sealed partial class CapabilityRules
{
    /// <summary>
    /// The gates and evidence that guard a point in a recipe. <see cref="Union"/> is what holds after crossing more
    /// guards (a gate met twice keeps the higher threshold); <see cref="Meet"/> is what holds on both of two ways in
    /// (a gate on both keeps the lower threshold).
    /// </summary>
    /// <param name="Gates">The gates, each once.</param>
    /// <param name="Evidence">The evidence entries, each once.</param>
    private sealed record Guards(IReadOnlyList<GateGuard> Gates, IReadOnlyList<EvidenceCheck> Evidence)
    {
        public static Guards None { get; } = new([], []);

        public static Guards Of(RecipeTransition transition) => None.Union(new Guards(
            [.. transition.Gates.Select(g => new GateGuard(g.Use, g.With, g.Threshold))],
            [.. transition.Evidence]));

        public Guards Union(Guards other)
        {
            var gates = Gates.ToList();
            foreach (var gate in other.Gates)
            {
                var at = gates.FindIndex(g => g.SameGate(gate));
                if (at < 0)
                {
                    gates.Add(gate);
                }
                else
                {
                    gates[at] = gates[at] with { Threshold = Higher(gates[at].Threshold, gate.Threshold) };
                }
            }

            return new Guards(gates, [.. Evidence.Union(other.Evidence)]);
        }

        public Guards Meet(Guards other) => new(
            [.. Gates.SelectMany(g => other.Gates.Where(o => o.SameGate(g)).Take(1).Select(o => g with { Threshold = Lower(g.Threshold, o.Threshold) }))],
            [.. Evidence.Intersect(other.Evidence)]);

        public bool SameAs(Guards other) =>
            Gates.Count == other.Gates.Count
            && Gates.All(g => other.Gates.Any(o => o.SameGate(g) && o.Threshold == g.Threshold))
            && Evidence.Count == other.Evidence.Count
            && Evidence.All(other.Evidence.Contains);

        private static double? Higher(double? a, double? b) => a is { } x && b is { } y ? Math.Max(x, y) : a ?? b;

        private static double? Lower(double? a, double? b) => a is { } x && b is { } y ? Math.Min(x, y) : null;
    }
}
