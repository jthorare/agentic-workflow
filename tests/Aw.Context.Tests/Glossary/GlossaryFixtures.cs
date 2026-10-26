using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Glossary;

internal static class GlossaryFixtures
{
    internal static FeatureVectors Feature(string key, string label, params (string Name, float[] Vector)[] symbols) =>
        new(NodeKey.From(key), label, [.. symbols.Select(s => new SymbolVector(s.Name, s.Vector))]);

    internal static IReadOnlyList<string> Keys(IEnumerable<NodeKey> keys) => [.. keys.Select(k => k.Value)];

    internal static MapGlossary Build(IReadOnlyList<FeatureVectors> features, GlossaryRules? rules = null) =>
        GlossaryBuilder.Build(features, rules ?? new GlossaryRules());

    // Two feature groups on separate axes: billing on x, scheduling on y; each has distinct words and one shared word.
    internal static List<FeatureVectors> TwoGroups() =>
    [
        Feature("feature:billing.invoice", "Invoice totals", ("InvoiceTotal", [1f, 0.1f, 0f]), ("computeShared", [1f, 0f, 0f])),
        Feature("feature:billing.refund", "Refund flow", ("refund_payment", [1f, 0.2f, 0f]), ("computeShared", [1f, 0f, 0f])),
        Feature("feature:scheduling.shift", "Shift swap", ("ShiftSwap", [0f, 1f, 0.1f]), ("computeShared", [0f, 1f, 0f])),
    ];
}
