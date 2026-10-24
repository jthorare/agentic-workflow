using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Graph;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

public sealed class LensesSetTests
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string Groups = "feature:tabs.tab-groups";
    private const string Layout = "thing:pane-layout";
    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset _created = DateTimeOffset.Parse("2026-10-12T09:00:00Z", CultureInfo.InvariantCulture);

    private static GraphSnapshot Tabs()
    {
        var map = Maps.Checked(Maps.Parse(MapTabsRepo.MapJson));
        return OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
    }

    private static LensBasis Basis(IReadOnlyList<string>? files = null) =>
        new("v2", Draft: false, _asOf) { MapCreated = _created, Files = files ?? [] };

    private static LensesSetView Set(GraphSnapshot graph, string[] anchors, int depth = 2, LensBasis? basis = null) =>
        LensesBuilder.BuildSet(basis ?? Basis(), graph, anchors, depth).Value;

    private static LensesView One(GraphSnapshot graph, string anchor, int depth = 2, LensBasis? basis = null) =>
        LensesBuilder.Build(basis ?? Basis(), graph, NodeKey.From(anchor), depth).Value;

    private static string[] Keys(IEnumerable<LensItem> items) => [.. items.Select(i => i.Node.Value)];

    [Fact, Trait("Lane", "unit")]
    public void Two_anchors_answer_schema_2_with_anchors_in_place_of_anchor()
    {
        var view = Set(Tabs(), [Manager, Layout]);

        Assert.Equal((2, "v2", false, 2, _asOf), (view.SchemaVersion, view.MapVersion, view.Draft, view.Depth, view.AsOf));
        Assert.Equal([Manager, Layout], view.Anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
        Assert.Empty(view.MissingAnchors);
        Assert.All(view.Anchors, a => Assert.False(string.IsNullOrEmpty(a.Label)));
    }

    [Fact, Trait("Lane", "unit")]
    public void One_anchor_keeps_schema_1_and_its_golden_fields()
    {
        var view = One(Tabs(), Manager);

        Assert.Equal((1, Manager), (view.SchemaVersion, view.Anchor.Key.Value));
        Assert.Null(view.Coverage.ByAnchor);
        Assert.All(view.Bands.Product.Own, i => Assert.Null(i.Anchors));
        Assert.NotNull(view.Searched);
        Assert.NotNull(view.Coverage.Map);
    }

    [Fact, Trait("Lane", "unit")]
    public void Duplicate_anchor_values_are_one_anchor()
    {
        var view = Set(Tabs(), [Manager, Manager]);

        Assert.Equal([Manager], view.Anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
        Assert.Equal([Manager], view.Searched.Anchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_values_naming_one_node_are_one_anchor_in_a_set()
    {
        var view = Set(Tabs(), [Manager, "Tab manager"]);

        _ = Assert.Single(view.Anchors);
        Assert.Empty(view.MissingAnchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Item_reached_by_two_anchors_lists_both_in_request_order()
    {
        var view = Set(Tabs(), [Groups, Manager], depth: 0);

        var surface = view.Bands.Product.Own.Single(i => i.Node.Value == "surface:command/group-tabs");
        Assert.Equal([Groups, Manager], surface.Anchors);
        var onlyManager = view.Bands.Product.Own.Single(i => i.Node.Value == "surface:command/switch-tab");
        Assert.Equal([Manager], onlyManager.Anchors);
        Assert.All(view.Bands.Product.Own, i => Assert.Equal(0, i.Depth));
        var reversed = Set(Tabs(), [Manager, Groups], depth: 0).Bands.Product.Own.Single(i => i.Node.Value == "surface:command/group-tabs");
        Assert.Equal([Manager, Groups], reversed.Anchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Product_node_own_for_one_anchor_and_affects_for_another_is_in_own_once()
    {
        var view = Set(Tabs(), [Manager, Layout]);

        const string restore = "action:tab-manager.restore-layout";
        Assert.Single(Keys(view.Bands.Product.Own), restore);
        Assert.DoesNotContain(view.Bands.Product.Affects, a => a.Node.Value == restore);
        Assert.DoesNotContain(view.Bands.Product.Neighbors, a => a.Node.Value == restore);
        var item = view.Bands.Product.Own.Single(i => i.Node.Value == restore);
        Assert.Equal([Manager, Layout], item.Anchors);
        Assert.Equal(0, item.Depth);
    }

    [Fact, Trait("Lane", "unit")]
    public void Coverage_is_over_the_union_and_byAnchor_matches_each_single_anchor_view()
    {
        var graph = Tabs();

        var view = Set(graph, [Groups, Manager]);

        Assert.Equal(One(graph, Manager).Coverage.Scope, view.Coverage.Scope);
        Assert.Equal([Groups, Manager], view.Coverage.ByAnchor!.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var key in new[] { Groups, Manager })
        {
            var single = One(graph, key).Coverage;
            var by = view.Coverage.ByAnchor![key];
            Assert.Equivalent(single with { Map = null }, by);
            Assert.Null(by.ByAnchor);
            Assert.Null(by.Map);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Union_band_over_100_lists_every_item_by_min_depth_then_anchor_order()
    {
        var builder = new GraphBuilder().Node("feature:a").Node("feature:b");
        for (var i = 0; i < 70; i++)
        {
            var n = i.ToString("D3", CultureInfo.InvariantCulture);
            _ = builder.Edge("feature:a-" + n, EdgeType.PartOf, "feature:a").Edge("feature:b-" + n, EdgeType.PartOf, "feature:b");
        }

        var product = Set(builder.Build(), ["feature:b", "feature:a"], depth: 0).Bands.Product;

        Assert.Equal((142, 142), (product.Own.Count, product.Counts.Own));
        Assert.All(product.Own.Take(71), i => Assert.Equal(["feature:b"], i.Anchors));
        Assert.All(product.Own.Skip(71), i => Assert.Equal(["feature:a"], i.Anchors));
        Assert.Equal(product.Own.Skip(71).Select(i => i.Node.Value).Order(StringComparer.Ordinal), product.Own.Skip(71).Select(i => i.Node.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_neighbor_takes_the_minimum_depth_over_the_anchors_that_reached_it()
    {
        var graph = new GraphBuilder().Node("feature:a").Node("feature:b").Edge("feature:n", EdgeType.DependsOn, "feature:m").Edge("feature:m", EdgeType.DependsOn, "feature:a")
            .Edge("feature:n", EdgeType.DependsOn, "feature:b").Build();

        var view = Set(graph, ["feature:a", "feature:b"], depth: 3);

        var all = view.Bands.Product.Neighbors.Concat(view.Bands.Product.Affects).Where(r => r.Node.Value == "feature:n").ToList();
        var n = Assert.Single(all);
        Assert.Equal(1, n.Depth);
        Assert.Equal(["feature:a", "feature:b"], n.Anchors!.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Unknown_anchor_in_a_set_is_in_missingAnchors_as_given_and_the_rest_answer()
    {
        var view = Set(Tabs(), ["nope:x", Manager, "nope:y"]);

        Assert.Equal(["nope:x", "nope:y"], view.MissingAnchors);
        Assert.Equal([Manager], view.Anchors.Select(a => a.Key.Value), StringComparer.Ordinal);
        Assert.Equal([Manager], view.Searched.Anchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_anchor_resolving_is_003_with_suggestedAnchors()
    {
        var result = LensesBuilder.BuildSet(Basis(), Tabs(), ["tab manager restore", "nope:x"], 2);

        Assert.Equal(ContextErrors.UnknownAnchor.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ambiguous_name_in_a_set_refuses_037()
    {
        var graph = new GraphBuilder().Node("feature:a", "Layout").Node("feature:b", "Layout").Node("feature:c", "Other").Build();

        var result = LensesBuilder.BuildSet(Basis(), graph, ["feature:c", "Layout"], 2);

        Assert.Equal(ContextErrors.AmbiguousAnchor.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_anchors_is_006_and_any_number_of_anchors_is_not()
    {
        var many = Enumerable.Range(0, 50).Select(i => "nope:" + i.ToString(CultureInfo.InvariantCulture)).ToArray();

        Assert.Equal(ContextErrors.QueryInvalid.Code, LensesBuilder.BuildSet(Basis(), Tabs(), [], 2).Error.Code);
        Assert.Null(QueryRules.CheckAnchors(many));
        Assert.Equal(ContextErrors.UnknownAnchor.Code, LensesBuilder.BuildSet(Basis(), Tabs(), many, 2).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Fifty_distinct_anchors_answer_a_full_schema_2_set()
    {
        var graph = Tabs();
        var extra = Enumerable.Range(0, 50 - graph.Nodes.Count).Select(i => new GraphNode(NodeKey.From("thing:cap-" + i.ToString(CultureInfo.InvariantCulture)), "Cap " + i.ToString(CultureInfo.InvariantCulture)));
        var wide = graph with { Nodes = [.. graph.Nodes, .. extra] };
        var keys = wide.Nodes.Take(50).Select(n => n.Key.Value).ToArray();

        var view = Set(wide, keys);

        Assert.Equal((2, 50), (view.SchemaVersion, view.Anchors.Count));
        Assert.Empty(view.MissingAnchors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Map_coverage_counts_the_files_under_the_anchors_paths()
    {
        var basis = Basis(["src/tabs/TabManager.ts", "src/tabs/TabGroups.ts", "src/tabs/Pinned.ts", "src/other/Elsewhere.ts"]);

        var map = One(Tabs(), Manager, basis: basis).Coverage.Map!;

        Assert.Equal((3, 1, 1, false), (map.Files, map.Unmapped, map.Suggested, map.Truncated));
    }

    [Fact, Trait("Lane", "unit")]
    public void Searched_lists_map_intent_sources_and_code_index_with_states_codes_and_reasons()
    {
        var onyx = new SearchedSource(SourceId.From("onyx"), SearchedState.Degraded, Reason: null, "SND-CONTEXT-010", AsOf: null);
        var basis = Basis() with { IntentSources = [onyx] };

        var searched = Set(Tabs(), [Manager, Layout], basis: basis).Searched;

        Assert.Equal([Manager, Layout], searched.Anchors);
        Assert.Equal(("v2", 2), (searched.MapVersion, searched.Depth));
        Assert.Equal(["state", "dependency", "flow"], searched.Hops);
        Assert.Equal(
            [(SourceId.From("map"), SearchedState.Ok, null, null, _created), (SourceId.From("onyx"), SearchedState.Degraded, null, "SND-CONTEXT-010", null), (SourceId.From("code-index"), SearchedState.Unavailable, "index-not-built", null, null)],
            searched.Sources.Select(s => (s.Id, s.State, s.Reason, s.Code, s.AsOf)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_failed_intent_source_reads_degraded_on_the_wire_with_its_code()
    {
        var row = SearchedSources.Intent(SourceId.From("onyx"), configured: true, new IntentSourceState("c", _created, LastError: "SND-CONTEXT-010", 3, 1, 0, 0));

        Assert.Equal("\"degraded\"", System.Text.Json.JsonSerializer.Serialize(row.State));
        Assert.Equal(("SND-CONTEXT-010", SearchedState.Degraded), (row.Code, row.State));
    }

    [Fact, Trait("Lane", "unit")]
    public void Intent_source_rows_read_not_configured_not_synced_degraded_and_ok()
    {
        var synced = new IntentSourceState("c", _created, LastError: null, 3, 1, 0, 0);

        Assert.Equal((SearchedState.NotConfigured, "not-configured", null), Parts(SearchedSources.Intent(SourceId.From("onyx"), configured: false, state: null)));
        Assert.Equal((SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161"), Parts(SearchedSources.Intent(SourceId.From("onyx"), configured: true, state: null)));
        Assert.Equal((SearchedState.Unavailable, "not-synced", "SND-CONTEXT-161"), Parts(SearchedSources.Intent(SourceId.From("onyx"), configured: true, new IntentSourceState(Cursor: null, LastSync: null, LastError: null, Records: 0, Links: 0, Decisions: 0, Queued: 0))));
        Assert.Equal((SearchedState.Degraded, null, "SND-CONTEXT-010"), Parts(SearchedSources.Intent(SourceId.From("onyx"), configured: true, synced with { LastError = "SND-CONTEXT-010" })));
        Assert.Equal((SearchedState.Ok, null, "SND-CONTEXT-059"), Parts(SearchedSources.Intent(SourceId.From("onyx"), configured: true, synced with { LastError = "SND-CONTEXT-059" })));
        var ok = SearchedSources.Intent(SourceId.From("onyx"), configured: true, synced);
        Assert.Equal((SearchedState.Ok, null, null, _created), (ok.State, ok.Reason, ok.Code, ok.AsOf));
    }

    private static (SearchedState, string?, string?) Parts(SearchedSource s) => (s.State, s.Reason, s.Code);

    [Fact, Trait("Lane", "unit")]
    public void Band_empty_flags_match_band_items()
    {
        var graph = new GraphBuilder().Node("feature:lonely").Build();

        var empty = One(graph, "feature:lonely", depth: 0).Searched.Bands;
        var full = One(Tabs(), Manager).Searched.Bands;

        Assert.Equal((true, false, true), (empty.Intent.Empty, empty.Product.Empty, empty.Code.Empty));
        Assert.Equal((false, false, false), (full.Intent.Empty, full.Product.Empty, full.Code.Empty));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_band_item_carries_label_kind_and_trust()
    {
        var view = Set(Tabs(), [Manager, Groups]);

        Assert.All(view.Bands.Intent.Records, r => Assert.Equal((NodeKind.Record, false), (r.Kind, string.IsNullOrEmpty(r.Label))));
        Assert.All(view.Bands.Intent.Decisions, d => Assert.Equal((NodeKind.Decision, false), (d.Kind, string.IsNullOrEmpty(d.Label))));
        Assert.All(view.Bands.Code.Files, f => Assert.Equal((NodeKind.Code, f.Path), (f.Kind, f.Label)));
        var suggested = view.Bands.Code.Files.Single(f => f.Suggested);
        var confirmed = view.Bands.Code.Files.First(f => f.Confirmed);
        Assert.Equal((Context.Core.Results.Trust.Untrusted, Context.Core.Results.Trust.Trusted), (suggested.Trust, confirmed.Trust));
    }

    [Fact, Trait("Lane", "unit")]
    public void Files_and_records_in_a_set_list_the_anchors_that_reached_them_and_depth_0()
    {
        var view = Set(Tabs(), [Groups, Manager]);

        var file = view.Bands.Code.Files.Single(f => f.Path == "src/tabs/TabManager.ts");
        Assert.Equal([Manager], file.Anchors);
        Assert.Equal(0, file.Depth);
        var decision = Assert.Single(view.Bands.Intent.Decisions, d => d.Decides.Any(k => k.Value == Groups) && d.Anchors!.Count > 0 && d.Supersedes.Count > 0);
        Assert.Equal([Groups, Manager], decision.Anchors);
        _ = Assert.Single(view.Conflicts);
    }

    private static string[] TabFiles => ["src/tabs/TabManager.ts", "src/tabs/TabGroups.ts", "src/tabs/Extra.ts", "src/tabs/deep/More.ts", "src/other/Z.ts", "README.md"];

    [Fact, Trait("Lane", "unit")]
    public void Map_coverage_counts_equal_filemap_totals_under_the_anchor_paths()
    {
        var graph = Tabs();

        var map = One(graph, Manager, basis: Basis(TabFiles)).Coverage.Map!;

        var filemap = FileMapBuilder.Build(new FileMapBasis("v2", "abc1234"), new FileMapQuery(ContextRepoName.From("aw-split-panes"), "src/tabs", UnmappedOnly: false, Draft: false), TabFiles, graph).Value.Totals;
        Assert.Equal(["src/tabs/"], map.Paths);
        Assert.Equal((filemap.Files, filemap.Unmapped, filemap.Suggested), (map.Files, map.Unmapped, map.Suggested));
        Assert.Equal((4, 2, 1), (map.Files, map.Unmapped, map.Suggested));
        Assert.Equal((_created, 2, false), (map.Created, map.AgeDays, map.Truncated));
    }

    [Fact, Trait("Lane", "unit")]
    public void Map_coverage_paths_cap_at_50_with_truncated()
    {
        var builder = new GraphBuilder().Node("feature:wide");
        var files = new List<string>();
        for (var i = 0; i < 60; i++)
        {
            var path = $"src/d{i.ToString("D2", CultureInfo.InvariantCulture)}/F.ts";
            files.Add(path);
            _ = builder.Edge("code:" + path, EdgeType.Implements, "feature:wide");
        }

        var map = One(builder.Build(), "feature:wide", depth: 0, basis: Basis(files)).Coverage.Map!;

        Assert.Equal((50, true), (map.Paths.Count, map.Truncated));
        Assert.Equal("src/d00/", map.Paths[0]);
        Assert.Equal(50, map.Files);
    }

    [Fact, Trait("Lane", "unit")]
    public void Set_map_coverage_is_over_the_union_of_own_scopes()
    {
        var map = Set(Tabs(), [Manager, Groups], basis: Basis(TabFiles)).Coverage.Map!;

        Assert.Equal(["src/tabs/"], map.Paths);
    }
}
