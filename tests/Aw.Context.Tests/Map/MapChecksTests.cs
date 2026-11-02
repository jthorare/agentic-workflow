using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Map;

namespace Aw.Context.Tests.Map;

/// <summary>Map structure spec §4.4: each measure at its exact boundary on a graph built for it. "At the limit" passes.</summary>
public sealed class MapChecksTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(50, CheckVerdict.Pass)]        // 5% of 1000 features
    [InlineData(51, CheckVerdict.Degraded)]    // 5.1%
    [InlineData(600, CheckVerdict.Degraded)]   // 60%
    [InlineData(601, CheckVerdict.Refuse)]     // 60.1%
    public void Isolated_features_pass_at_5_percent_degrade_above_and_refuse_above_60(int isolated, CheckVerdict expected)
    {
        var report = MapChecks.Measure(StructureGraphs.Isolated(total: 1000, isolated));

        Assert.Equal((isolated, 1000, expected), (report.Isolated.Count, report.Isolated.Total, report.Isolated.Verdict));
        Assert.Equal(CheckVerdict.Pass, report.OverCap.Verdict);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(20, CheckVerdict.Pass)]        // 2% of 1000 features
    [InlineData(21, CheckVerdict.Degraded)]    // 2.1%
    [InlineData(100, CheckVerdict.Degraded)]   // 10%
    [InlineData(101, CheckVerdict.Refuse)]     // 10.1%
    public void Features_over_the_cap_pass_at_2_percent_degrade_above_and_refuse_above_10(int oversized, CheckVerdict expected)
    {
        var report = MapChecks.Measure(StructureGraphs.Oversized(total: 1000, oversized));

        Assert.Equal((oversized, 1000, expected), (report.OverCap.Count, report.OverCap.Total, report.OverCap.Verdict));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_of_exactly_the_cap_is_not_over_it()
    {
        var report = MapChecks.Measure(StructureGraphs.Of([MapChecks.SizeCap, MapChecks.SizeCap + 1], isolated: 0));

        Assert.Equal((1, 2), (report.OverCap.Count, report.OverCap.Total));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fewer_than_twenty_features_of_five_files_skip_the_isolation_measure_without_a_note()
    {
        var report = MapChecks.Measure(StructureGraphs.Isolated(total: 19, isolated: 19));

        Assert.NotNull(report.Isolated.Skipped);
        Assert.Equal(CheckVerdict.Pass, report.Isolated.Verdict);
        Assert.False(report.NoFileEdges);
        Assert.Empty(report.Decide(ChecksMode.Refuse).Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Features_under_five_files_are_not_counted_for_isolation()
    {
        var report = MapChecks.Measure(StructureGraphs.Isolated(total: 40, isolated: 40, filesEach: 4));

        Assert.NotNull(report.Isolated.Skipped);
        Assert.Equal(CheckVerdict.Pass, report.Isolated.Verdict);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_with_no_file_edges_skips_the_isolation_measure_and_says_so_and_is_never_refused()
    {
        var report = MapChecks.Measure(StructureGraphs.Isolated(total: 40, isolated: 40, fileEdges: false));

        var outcome = report.Decide(ChecksMode.Refuse);

        Assert.NotNull(report.Isolated.Skipped);
        Assert.True(report.NoFileEdges);
        Assert.Equal((CheckVerdict.Degraded, null), (outcome.Verdict, outcome.Refusal));
        Assert.Equal(["structure: no file edges (imports and data stores not extracted)"], outcome.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_code_to_table_read_alone_is_a_file_edge_so_the_isolation_measure_applies()
    {
        var graph = StructureGraphs.Isolated(total: 40, isolated: 40, fileEdges: false);
        var table = NodeKey.From("thing:orders");
        graph = graph with
        {
            Nodes = [.. graph.Nodes, new GraphNode(table, "orders", Context.Core.Results.Provenance.Deterministic)],
            Edges = [.. graph.Edges, new GraphEdge(NodeKey.From("code:pkg0/f0.ts"), table, EdgeType.Reads, Context.Core.Results.Provenance.Deterministic)],
        };

        var report = MapChecks.Measure(graph);

        Assert.False(report.NoFileEdges);
        Assert.Null(report.Isolated.Skipped);
        Assert.Equal((40, 40, CheckVerdict.Refuse), (report.Isolated.Count, report.Isolated.Total, report.Isolated.Verdict));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_map_with_no_home_features_passes_with_nothing_to_say()
    {
        var report = MapChecks.Measure(GraphSnapshot.Empty);
        var outcome = report.Decide(ChecksMode.Refuse);

        Assert.Equal((0, 0, 0), (report.Isolated.Total, report.OverCap.Total, report.Offenders.Count));
        Assert.Equal((CheckVerdict.Pass, null, null), (outcome.Verdict, outcome.Refusal, outcome.Stored));
        Assert.Empty(outcome.Notes);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_deterministic_edges_count_and_a_group_feature_with_no_files_is_not_a_leaf()
    {
        var graph = StructureGraphs.Isolated(total: 20, isolated: 0);
        graph = graph with { Edges = [.. graph.Edges.Select(e => e.Type == EdgeType.DependsOn && e.From.Kind == NodeKind.Feature ? e with { Provenance = Context.Core.Results.Provenance.Model } : e)] };

        var report = MapChecks.Measure(graph);

        Assert.Equal((20, 20), (report.Isolated.Count, report.Isolated.Total));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_degraded_note_and_the_stored_reason_carry_numbers_and_limits_only()
    {
        var outcome = MapChecks.Measure(StructureGraphs.Isolated(total: 100, isolated: 41)).Decide(ChecksMode.Refuse);

        Assert.Equal(CheckVerdict.Degraded, outcome.Verdict);
        Assert.Equal(["structure: 41% of features (a feature is a group of files) have no link to another feature or a table; limit 5%"], outcome.Notes);
        Assert.Equal("structure 41% isolated, limit 5%", outcome.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public void Several_degraded_measures_join_with_a_semicolon()
    {
        // 100 features: 5 over the cap, 41 (the first ones, the 5 big among them) with no link.
        var graph = StructureGraphs.Of([.. Enumerable.Range(0, 100).Select(i => i < 5 ? 151 : 5)], isolated: 41);

        var outcome = MapChecks.Measure(graph).Decide(ChecksMode.Refuse);

        Assert.Equal("structure 41% isolated, limit 5%; size 5% over 150 files, limit 2%", outcome.Stored);
        Assert.Equal(2, outcome.Notes.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public void Refusal_names_the_code_and_the_numbers_and_warn_turns_it_into_a_note()
    {
        var report = MapChecks.Measure(StructureGraphs.Isolated(total: 100, isolated: 70));

        var refused = report.Decide(ChecksMode.Refuse);
        var warned = report.Decide(ChecksMode.Warn);

        Assert.Equal("SND-CONTEXT-060", refused.Refusal?.Code);
        Assert.Contains("70% of features", refused.Refusal?.Message, StringComparison.Ordinal);
        Assert.Contains("limit 60%", refused.Refusal?.Message, StringComparison.Ordinal);
        Assert.Equal((CheckVerdict.Degraded, null), (warned.Verdict, warned.Refusal));
        Assert.Equal("structure 70% isolated, limit 60%", warned.Stored);
    }

    [Fact, Trait("Lane", "unit")]
    public void Oversized_features_refuse_with_the_other_code()
    {
        var outcome = MapChecks.Measure(StructureGraphs.Oversized(total: 100, oversized: 11)).Decide(ChecksMode.Refuse);

        Assert.Equal("SND-CONTEXT-061", outcome.Refusal?.Code);
        Assert.Contains("11% of features hold more than 150 files (limit 10%)", outcome.Refusal?.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_offenders_are_the_five_largest_and_stay_out_of_the_stored_reason()
    {
        var report = MapChecks.Measure(StructureGraphs.Oversized(total: 100, oversized: 8));

        Assert.Equal(5, report.Offenders.Count(o => o.Measure == "over-cap"));
        Assert.All(report.Offenders, o => Assert.StartsWith("feature:pkg", o.Feature, StringComparison.Ordinal));
        Assert.DoesNotContain("pkg", report.Decide(ChecksMode.Refuse).Stored ?? string.Empty, StringComparison.Ordinal);
    }
}
