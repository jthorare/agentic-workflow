using System.Globalization;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Overrides;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Lenses;

/// <summary>A lenses fill reads the graph through a bounded number of index builds (issue 1101).</summary>
[Collection(GraphIndexBuilds.Name)]
public sealed class LensesSetReadCostTests
{
    private const int MaxBuildsPerFill = 2;

    private static readonly string[] _manyAnchors =
    [
        "feature:tabs.tab-manager",
        "feature:tabs.tab-groups",
        "thing:pane-layout",
        "surface:command/group-tabs",
        "surface:command/switch-tab",
        "action:tab-manager.restore-layout",
    ];

    private static readonly DateTimeOffset _asOf = DateTimeOffset.Parse("2026-10-14T09:12:40Z", CultureInfo.InvariantCulture);

    private static GraphSnapshot Tabs()
    {
        var map = Maps.Checked(Maps.Parse(MapTabsRepo.MapJson));
        return OverrideApplier.Apply(map.Graph, map.Overrides).Graph;
    }

    private static long BuildsDuring(GraphSnapshot graph, string[] anchors)
    {
        var basis = new LensBasis("v2", Draft: false, _asOf) { MapCreated = _asOf, Files = [] };
        var before = GraphIndex.Builds;
        _ = LensesBuilder.BuildSet(basis, graph, anchors, depth: 2);
        return GraphIndex.Builds - before;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lenses_fill_builds_the_graph_index_a_bounded_number_of_times_whatever_the_anchor_count()
    {
        var graph = Tabs();

        var one = BuildsDuring(graph, [_manyAnchors[0]]);
        var many = BuildsDuring(graph, _manyAnchors);

        Assert.True(many <= MaxBuildsPerFill, "a fill over several anchors built more graph indexes than the bound");
        Assert.True(many <= one, "graph index builds grew with the anchor count");
    }
}
