using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.ConceptScan;

public sealed class ConceptScanSummaryTests
{
    private static GraphSnapshot Map() => new GraphBuilder()
        .Node("feature:shifts.swap", "Swap shifts")
        .Node("feature:pay.run", "Run payroll")
        .Node("surface:command/swap", "Swap command")
        .Edge("code:src/shifts/swap.ts", EdgeType.Implements, "feature:shifts.swap")
        .Edge("code:src/shifts/board.ts", EdgeType.Implements, "feature:shifts.swap")
        .Build();

    [Fact, Trait("Lane", "unit")]
    public void The_feature_keys_are_only_features_in_key_order()
        => Assert.Equal(["feature:pay.run", "feature:shifts.swap"], ConceptScanSummary.FeatureKeys(Map()), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Each_feature_is_one_line_with_its_label_and_top_files()
    {
        var lines = ConceptScanSummary.Build(Map(), glossary: null, maxChars: 10_000).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["feature:pay.run | Run payroll", "feature:shifts.swap | Swap shifts | src/shifts/board.ts, src/shifts/swap.ts"], lines.Select(static l => l.TrimEnd('\r')), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Glossary_terms_follow_the_features_when_there_are_some()
    {
        var glossary = new MapGlossary([new GlossaryTerm("shift", 2.5, [NodeKey.From("feature:shifts.swap")])], [], [], Truncated: false);

        var text = ConceptScanSummary.Build(Map(), glossary, maxChars: 10_000);

        Assert.Contains("term shift: feature:shifts.swap", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("term shift", StringComparison.Ordinal) > text.IndexOf("feature:shifts.swap |", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_text_is_cut_at_a_line_end_under_the_limit_and_says_how_much_was_left_out()
    {
        var graph = Enumerable.Range(0, 200).Aggregate(new GraphBuilder(), static (b, i) => b.Node(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"feature:f{i}"), "A feature with a label")).Build();

        var text = ConceptScanSummary.Build(graph, glossary: null, maxChars: 1_000);

        Assert.True(text.Length <= 1_000);
        Assert.Contains("more lines not shown", text, StringComparison.Ordinal);
    }
}
