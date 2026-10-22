using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Core.Symbols;
using Aw.Context.Core;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Graph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

public sealed class LensesBuilderTests
{
    private const string Anchor = "feature:tabs.tab-manager";
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    private static GraphSnapshot Tabs()
    {
        var map = Maps.Checked(Maps.Parse(MapTabsRepo.MapJson));
        return OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
    }

    private static LensesView Build(GraphSnapshot graph, string anchor, int depth, bool draft = false, string map = "v13@7c3e1a2") =>
        LensesBuilder.Build(new LensBasis(map, draft, _asOf), graph, NodeKey.From(anchor), depth).Value;

    private static LensesView Lenses(string anchor = Anchor, int depth = 2) => Build(Tabs(), anchor, depth);

    private static string[] Keys(IEnumerable<NodeKey> keys) => [.. keys.Select(k => k.Value)];

    [Fact, Trait("Lane", "unit")]
    public void The_envelope_names_the_map_the_anchor_the_depth_and_when_and_has_no_cites_or_degraded_sources()
    {
        var view = Build(Tabs(), Anchor, depth: 2, draft: true);

        Assert.Equal((1, "v13@7c3e1a2", true, 2, _asOf), (view.SchemaVersion, view.MapVersion, view.Draft, view.Depth, view.AsOf));
        Assert.Equal((Anchor, "Tab manager", NodeKind.Feature, Trust.Trusted), (view.Anchor.Key.Value, view.Anchor.Label, view.Anchor.Kind, view.Anchor.Trust));
        Assert.Empty(view.Degraded);
        Assert.Empty(view.Cites);
        Assert.Equal((SymbolsState.Unavailable, "index not built", 0), (view.Bands.Code.Symbols.State, view.Bands.Code.Symbols.Reason, view.Bands.Code.Symbols.Items.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_product_band_holds_the_own_scope_with_kind_provenance_and_trust()
    {
        var own = Lenses().Bands.Product.Own;

        Assert.Equal(
            ["action:tab-groups.group-selected", "action:tab-manager.restore-layout", "feature:tabs.tab-groups", Anchor, "surface:command/group-tabs", "surface:command/switch-tab"],
            own.Select(i => i.Node.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var action = own.Single(i => i.Node.Value == "action:tab-manager.restore-layout");
        Assert.Equal((NodeKind.Action, Provenance.Human, 1.0, true, Trust.Trusted), (action.Kind, action.Provenance, action.Confidence, action.Confirmed, action.Trust));
        Assert.Equal("Restore a tab's pane layout", action.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_neighbor_carries_its_depth_and_the_hops_from_the_anchor_and_leaves_the_own_scope_out()
    {
        var product = Lenses(depth: 3).Bands.Product;

        var neighbor = Assert.Single(product.Neighbors);
        Assert.Equal(("thing:pane-layout", NodeKind.Thing, 3), (neighbor.Node.Value, neighbor.Kind, neighbor.Depth));
        Assert.Equal(["part-of-by surface:command/switch-tab", "on-by action:tab-manager.restore-layout", "reads thing:pane-layout"], neighbor.Path);
        Assert.DoesNotContain(product.Own, i => i.Node.Value == "thing:pane-layout");
        Assert.Empty(Lenses(depth: 2).Bands.Product.Neighbors);
    }

    [Fact, Trait("Lane", "unit")]
    public void At_depth_0_there_are_no_neighbors_and_no_affected_nodes()
    {
        var product = Lenses(depth: 0).Bands.Product;

        Assert.Empty(product.Neighbors);
        Assert.Empty(product.Affects);
        Assert.Equal(6, product.Counts.Own);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_the_cascade_reaches_is_an_affects_item_with_its_depth_and_path_and_a_neighbor_is_listed_once_as_a_neighbor()
    {
        var graph = new GraphBuilder()
            .Edge("surface:screen/s", EdgeType.PartOf, "feature:a").Edge("surface:screen/s", EdgeType.Writes, "thing:t")
            .Edge("action:r1", EdgeType.Reads, "thing:t").Edge("action:r1", EdgeType.On, "surface:screen/r").Edge("surface:screen/r", EdgeType.PartOf, "feature:r")
            .Edge("feature:d", EdgeType.DependsOn, "feature:a")
            .Build();

        var product = Build(graph, "feature:a", depth: 2).Bands.Product;

        var affected = product.Affects.Single(i => i.Node.Value == "feature:r");
        Assert.Equal(1, affected.Depth);
        Assert.NotEmpty(affected.Path);
        Assert.Equal(1, product.Neighbors.Count(i => i.Node.Value == "feature:d") + product.Affects.Count(i => i.Node.Value == "feature:d"));
        _ = Assert.Single(product.Neighbors, n => n.Node.Value == "feature:d");
        Assert.Equal(product.Affects.Count, product.Counts.Affects);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_product_band_carries_its_own_edges_between_product_items_and_none_to_other_bands()
    {
        var edges = Lenses().Bands.Product.Edges;

        Assert.Equal(5, edges.Count);
        Assert.All(edges, e => Assert.Contains(e.Type, new[] { EdgeType.PartOf, EdgeType.On, EdgeType.Reads, EdgeType.Writes, EdgeType.StepOf, EdgeType.DependsOn, EdgeType.MovesTo }));
        Assert.Contains(edges, e => e is { Type: EdgeType.PartOf } && e.From.Value == "feature:tabs.tab-groups" && e.To.Value == Anchor);
        Assert.DoesNotContain(edges, e => e.To.Value == "thing:pane-layout");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_intent_band_holds_the_records_that_mention_scope_nodes_with_their_source_and_untrusted_state()
    {
        var intent = Lenses().Bands.Intent;

        var record = Assert.Single(intent.Records);
        Assert.Equal(("record:linear:issue/0e5a1b00-0000-4000-8000-000000000203", "Tab manager spec", "linear"), (record.Key.Value, record.Label, record.Source));
        Assert.Equal([Anchor], Keys(record.Mentions));
        Assert.Equal((Trust.Untrusted, CitationState.Unchecked), (record.Trust, record.State));
        Assert.Empty(record.Cites);
        Assert.Equal((1, 2, null), (intent.Counts.Records, intent.Counts.Decisions, intent.Counts.Orphaned));
        Assert.False(intent.Truncated);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_carries_its_wording_what_it_decides_and_supersedes_and_its_stored_quote()
    {
        var decisions = Lenses().Bands.Intent.Decisions;

        Assert.Equal(2, decisions.Count);
        var newer = decisions.Single(d => d.Node.Value.StartsWith("decision:ef895edc", StringComparison.Ordinal));
        Assert.Equal("A tab group stays expanded; collapsing is a per-user setting.", newer.Text);
        Assert.Equal(["feature:tabs.tab-groups"], Keys(newer.Decides));
        Assert.StartsWith("decision:d0363522", Assert.Single(newer.Supersedes).Value, StringComparison.Ordinal);
        Assert.Null(newer.Quotes);
        Assert.Equal((Provenance.Human, true, Trust.Trusted), (newer.Provenance, newer.Confirmed, newer.Trust));
        Assert.Equal(DateTimeOffset.Parse("2026-07-20T10:00:00Z", CultureInfo.InvariantCulture), newer.At);
        Assert.Equal(("linear:issue/0e5a1b00-0000-4000-8000-000000000202", "a tab group stays expanded; collapsing is a per-user setting", Trust.Untrusted),
            (newer.Quote!.Record.Value, newer.Quote.Text, newer.Quote.Trust));
        Assert.Empty(decisions.Single(d => d.Node.Value.StartsWith("decision:d0363522", StringComparison.Ordinal)).Supersedes);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("linear:issue/abc", "linear")]
    [InlineData("linear:doc/abc", "linear:doc")]
    [InlineData("slack:C1/123", "slack")]
    [InlineData("onyx:doc-9", "onyx")]
    [InlineData("repo-docs:docs/a.md", "repo-doc")]
    public void A_records_source_comes_from_its_key(string key, string source)
    {
        var graph = new GraphBuilder().Node("feature:a").Node("record:" + key, "A record").Edge("record:" + key, EdgeType.Mentions, "feature:a").Build();

        var record = Assert.Single(Build(graph, "feature:a", depth: 1).Bands.Intent.Records);

        Assert.Equal(source, record.Source);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_code_band_has_one_entry_per_file_and_marks_the_suggested_one_with_its_link()
    {
        var code = Lenses().Bands.Code;

        Assert.Equal(["src/tabs/TabGroups.ts", "src/tabs/TabManager.ts"], code.Files.Select(f => f.Path), StringComparer.Ordinal);
        var suggested = code.Files[0];
        Assert.Equal((Provenance.Model, 0.74, false, true), (suggested.Provenance, suggested.Confidence, suggested.Confirmed, suggested.Suggested));
        Assert.Equal(["feature:tabs.tab-groups"], Keys(suggested.Implements));
        Assert.Empty(suggested.Tests);
        Assert.Equal(SampleLink("code:src/tabs/TabGroups.ts", "feature:tabs.tab-groups"), suggested.LinkId);
        var confirmed = code.Files[1];
        Assert.Equal((true, false, null), (confirmed.Confirmed, confirmed.Suggested, confirmed.LinkId));
        Assert.Equal(["action:tab-manager.restore-layout", Anchor], Keys(confirmed.Implements));
        Assert.Equal((2, 0, 1, false), (code.Counts.Files, code.Counts.Symbols, code.Counts.FeaturesWithNoCode, code.Truncated));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_that_only_tests_a_scope_node_lists_it_under_tests_and_is_not_counted_as_implementing()
    {
        var graph = new GraphBuilder().Node("feature:a")
            .Edge("code:src/a.test.ts", EdgeType.Tests, "feature:a", provenance: Provenance.Deterministic).Build();

        var view = Build(graph, "feature:a", depth: 1);

        var file = Assert.Single(view.Bands.Code.Files);
        Assert.Equal(["feature:a"], Keys(file.Tests));
        Assert.Empty(file.Implements);
        Assert.Equal(0, view.Coverage.Code.Implemented);
        Assert.Equal(1, view.Bands.Code.Counts.FeaturesWithNoCode);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cross_band_edges_carry_ids_provenance_and_confirmation()
    {
        var view = Lenses();

        Assert.Equal(new Dictionary<EdgeType, int> { [EdgeType.Mentions] = 1, [EdgeType.Decides] = 2, [EdgeType.Supersedes] = 1, [EdgeType.Implements] = 3 },
            view.Edges.GroupBy(e => e.Type).ToDictionary(g => g.Key, g => g.Count()));
        var model = Assert.Single(view.Edges, e => !e.Confirmed);
        Assert.Equal((EdgeType.Implements, "code:src/tabs/TabGroups.ts", 0.74, Provenance.Model), (model.Type, model.From.Value, model.Confidence, model.Provenance));
        Assert.Equal(SampleLink("code:src/tabs/TabGroups.ts", "feature:tabs.tab-groups"), model.Link);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_that_supersedes_another_on_a_scope_node_is_a_conflict_on_that_node()
    {
        var conflict = Assert.Single(Lenses().Conflicts);

        Assert.Equal((ConflictKind.Supersedes, "feature:tabs.tab-groups"), (conflict.Kind, conflict.Node.Value));
        Assert.StartsWith("decision:ef895edc", conflict.Decision.Value, StringComparison.Ordinal);
        Assert.StartsWith("decision:d0363522", conflict.Other, StringComparison.Ordinal);
        Assert.Equal(("decision:ef895edc supersedes decision:d0363522", 0, 0), (conflict.Summary, conflict.Intent.Count, conflict.Behavior.Count));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_anchor_with_no_decisions_has_no_conflicts()
    {
        var graph = new GraphBuilder().Edge("surface:screen/s", EdgeType.PartOf, "feature:a").Build();

        Assert.Empty(Build(graph, "feature:a", depth: 2).Conflicts);
    }

    [Fact, Trait("Lane", "unit")]
    public void Coverage_on_the_tabs_map_counts_confirmed_implements_only_and_the_scope_nodes_with_only_a_suggestion()
    {
        var coverage = Lenses().Coverage;

        Assert.Equal(6, coverage.Scope);
        Assert.Equal(new CoverageIntent(Covered: 2, Of: 6), coverage.Intent);
        Assert.Equal(new CoverageProduct(Confirmed: 6, Of: 6), coverage.Product);
        Assert.Equal(new CoverageCode(Implemented: 2, Of: 6, Level: "file", Suggested: 1), coverage.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Gaps_name_what_each_scope_node_lacks_in_key_order_with_a_note_when_it_has_the_other_lens()
    {
        var gaps = Lenses().Coverage.Gaps;

        Assert.Equal(
        [
            ("action:tab-groups.group-selected", [GapKind.Intent, GapKind.Code]),
            ("action:tab-manager.restore-layout", [GapKind.Intent]),
            ("feature:tabs.tab-groups", [GapKind.Code]),
            ("surface:command/group-tabs", [GapKind.Intent, GapKind.Code]),
            ("surface:command/switch-tab", [GapKind.Intent, GapKind.Code]),
        ], gaps.Select(g => (g.Node.Value, g.Missing.ToArray())));
        Assert.Null(gaps[0].Note);
        Assert.NotNull(gaps.Single(g => g.Node.Value == "feature:tabs.tab-groups").Note);
        Assert.NotNull(gaps.Single(g => g.Node.Value == "action:tab-manager.restore-layout").Note);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_anchor_coverage_function_gives_the_lenses_coverage_for_the_same_anchor()
    {
        var graph = Tabs();

        var direct = LensCoverageBuilder.ForAnchor(graph, NodeKey.From(Anchor));

        Assert.Equivalent(Lenses().Coverage with { Map = null }, direct);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_only_node_is_not_confirmed_in_the_coverage_or_trusted_on_the_wire()
    {
        var graph = new GraphBuilder().Node("feature:a").Node("feature:b", provenance: Provenance.Model).Edge("feature:b", EdgeType.PartOf, "feature:a").Build();

        var view = Build(graph, "feature:a", depth: 1);

        Assert.Equal((2, 1), (view.Coverage.Scope, view.Coverage.Product.Confirmed));
        var model = view.Bands.Product.Own.Single(i => i.Node.Value == "feature:b");
        Assert.Equal((false, Trust.Untrusted), (model.Confirmed, model.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_is_cut_at_100_items_and_says_so_and_the_counts_stay_full()
    {
        var builder = new GraphBuilder().Node("feature:root");
        for (var i = 0; i < 120; i++)
        {
            _ = builder.Edge("feature:child-" + i.ToString("D3", CultureInfo.InvariantCulture), EdgeType.PartOf, "feature:root");
        }

        var product = Build(builder.Build(), "feature:root", depth: 1).Bands.Product;

        Assert.Equal(LensesBuilder.BandCap, product.Own.Count);
        Assert.True(product.Truncated);
        Assert.Equal(121, product.Counts.Own);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_alias_anchors_the_node_it_names_and_an_unknown_anchor_is_003()
    {
        var graph = new GraphBuilder().Node("feature:a").Alias("feature:old-a", "feature:a").Build();

        Assert.Equal("feature:a", Build(graph, "feature:old-a", depth: 1).Anchor.Key.Value);
        Assert.Equal(ContextErrors.UnknownAnchor.Code, LensesBuilder.Build(new LensBasis("v1@a", Draft: false, _asOf), graph, NodeKey.From("feature:nope"), 1).Error.Code);
    }

    private static LinkId SampleLink(string from, string to) => LinkIds.For(new GraphEdge(NodeKey.From(from), NodeKey.From(to), EdgeType.Implements));
}
