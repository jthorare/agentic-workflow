using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;

namespace Aw.Context.Tests.WholeMap;

public sealed class MapAreasTests
{
    private static readonly string[] _chain = ["feature:a", "feature:a.b", "feature:a.b.c", "feature:z"];
    private static readonly string[] _cycle = ["feature:m", "feature:b", "feature:c", "feature:leaf"];
    private static readonly string[] _umbrella = ["feature:u", "surface:screen/own"];
    private static readonly string[] _loose = ["record:lonely", "code:orphan.ts", "surface:screen/s", "action:do", "decision:d"];

    private static string AreaOf(AreaIndex index, string key) => index.ByNode[key].Area;

    [Fact, Trait("Lane", "unit")]
    public void A_feature_in_no_other_feature_is_its_own_area_and_a_child_takes_the_root_of_its_chain()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:a.b", "feature:a.b.c", "feature:z")
            .Edge("feature:a.b", EdgeType.PartOf, "feature:a").Edge("feature:a.b.c", EdgeType.PartOf, "feature:a.b").Build();

        var index = MapAreas.Assign(graph);

        Assert.Equal("feature:a feature:a feature:a feature:z", string.Join(' ', _chain.Select(k => AreaOf(index, k))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cycle_of_part_of_roots_at_its_smallest_key_and_ends()
    {
        var graph = new AreaGraph().Node("feature:m", "feature:b", "feature:c", "feature:leaf")
            .Edge("feature:m", EdgeType.PartOf, "feature:b").Edge("feature:b", EdgeType.PartOf, "feature:c").Edge("feature:c", EdgeType.PartOf, "feature:m")
            .Edge("feature:leaf", EdgeType.PartOf, "feature:c").Build();

        var index = MapAreas.Assign(graph);

        Assert.All(_cycle, k => Assert.Equal("feature:b", AreaOf(index, k)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Surfaces_follow_their_feature_actions_their_surface_and_flows_most_of_their_steps()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:b", "surface:screen/s1", "surface:screen/s2", "surface:screen/s3", "action:do", "flow:f")
            .Edge("surface:screen/s1", EdgeType.PartOf, "feature:a").Edge("surface:screen/s2", EdgeType.PartOf, "feature:b").Edge("surface:screen/s3", EdgeType.PartOf, "feature:b")
            .Edge("action:do", EdgeType.On, "surface:screen/s2")
            .Edge("surface:screen/s1", EdgeType.StepOf, "flow:f").Edge("surface:screen/s2", EdgeType.StepOf, "flow:f").Edge("surface:screen/s3", EdgeType.StepOf, "flow:f").Build();

        var index = MapAreas.Assign(graph);

        Assert.Equal(("feature:a", "feature:b", "feature:b"), (AreaOf(index, "surface:screen/s1"), AreaOf(index, "action:do"), AreaOf(index, "flow:f")));
        Assert.Equal("feature:a", string.Join(' ', index.ByNode["flow:f"].AlsoIn));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_thing_takes_the_area_of_the_surfaces_and_actions_that_read_or_write_it_most()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:b", "surface:screen/s1", "surface:screen/s2", "action:save", "thing:doc")
            .Edge("surface:screen/s1", EdgeType.PartOf, "feature:a").Edge("surface:screen/s2", EdgeType.PartOf, "feature:b").Edge("action:save", EdgeType.On, "surface:screen/s2")
            .Edge("surface:screen/s1", EdgeType.Reads, "thing:doc").Edge("surface:screen/s2", EdgeType.Reads, "thing:doc").Edge("action:save", EdgeType.Writes, "thing:doc").Build();

        Assert.Equal("feature:b", AreaOf(MapAreas.Assign(graph), "thing:doc"));
    }

    [Fact, Trait("Lane", "unit")]
    public void Code_takes_the_area_of_what_it_implements_or_tests_most_edges_first()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:b", "code:x.ts", "code:y.ts")
            .Edge("code:x.ts", EdgeType.Implements, "feature:a").Edge("code:x.ts", EdgeType.Implements, "feature:b").Edge("code:x.ts", EdgeType.Tests, "feature:b")
            .Edge("code:y.ts", EdgeType.Tests, "feature:a").Build();

        var index = MapAreas.Assign(graph);

        Assert.Equal(("feature:b", "feature:a"), (AreaOf(index, "code:x.ts"), AreaOf(index, "code:y.ts")));
        Assert.Equal("feature:a", string.Join(' ', index.ByNode["code:x.ts"].AlsoIn));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_follows_what_it_decides_a_record_what_it_mentions_and_a_decision_that_only_quotes_follows_its_records()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:b", "decision:d1", "decision:d2", "record:r1", "record:r2")
            .Edge("decision:d1", EdgeType.Decides, "feature:a").Edge("record:r1", EdgeType.Mentions, "feature:b").Edge("record:r2", EdgeType.Mentions, "feature:b")
            .Edge("decision:d2", EdgeType.Quotes, "record:r1").Edge("decision:d2", EdgeType.Quotes, "record:r2").Build();

        var index = MapAreas.Assign(graph);

        Assert.Equal(("feature:a", "feature:b", "feature:b"), (AreaOf(index, "decision:d1"), AreaOf(index, "record:r1"), AreaOf(index, "decision:d2")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_that_mentions_a_decision_takes_the_decisions_area_once_that_is_known()
    {
        var graph = new AreaGraph().Node("feature:a", "decision:d1", "record:r1")
            .Edge("decision:d1", EdgeType.Decides, "feature:a").Edge("record:r1", EdgeType.Mentions, "decision:d1").Build();

        Assert.Equal("feature:a", AreaOf(MapAreas.Assign(graph), "record:r1"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tie_goes_to_the_smallest_key_and_the_other_area_is_also_in()
    {
        var graph = new AreaGraph().Node("feature:b", "feature:a", "code:x.ts")
            .Edge("code:x.ts", EdgeType.Implements, "feature:b").Edge("code:x.ts", EdgeType.Implements, "feature:a").Build();

        var node = MapAreas.Assign(graph).ByNode["code:x.ts"];

        Assert.Equal(("feature:a", "feature:b"), (node.Area, string.Join(' ', node.AlsoIn)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Also_in_holds_at_most_five_areas_most_edges_first()
    {
        var builder = new AreaGraph().Node("code:x.ts");
        for (var i = 0; i < 8; i++)
        {
            var feature = "feature:f" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ = builder.Node(feature).Edge("code:x.ts", EdgeType.Implements, feature);
            if (i == 7)
            {
                _ = builder.Edge("code:x.ts", EdgeType.Tests, feature);
            }
        }

        var node = MapAreas.Assign(builder.Build()).ByNode["code:x.ts"];

        Assert.Equal("feature:f7", node.Area);
        Assert.Equal("feature:f0 feature:f1 feature:f2 feature:f3 feature:f4", string.Join(' ', node.AlsoIn));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_no_rule_reaches_is_unassigned_and_so_is_what_hangs_only_on_it()
    {
        var graph = new AreaGraph().Node("feature:a", "record:lonely", "code:orphan.ts", "surface:screen/s", "action:do", "decision:d")
            .Edge("action:do", EdgeType.On, "surface:screen/s").Edge("decision:d", EdgeType.Decides, "code:orphan.ts").Build();

        var index = MapAreas.Assign(graph);

        Assert.All(_loose, k => Assert.Equal(AreaIndex.UnassignedKey, AreaOf(index, k)));
        Assert.Equal("feature:a", AreaOf(index, "feature:a"));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_same_graph_gives_the_same_areas_whatever_the_order_of_its_rows()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:b", "feature:b.c", "surface:screen/s", "code:x.ts", "record:r", "decision:d")
            .Edge("feature:b.c", EdgeType.PartOf, "feature:b").Edge("surface:screen/s", EdgeType.PartOf, "feature:b.c").Edge("code:x.ts", EdgeType.Implements, "feature:a")
            .Edge("code:x.ts", EdgeType.Implements, "surface:screen/s").Edge("record:r", EdgeType.Mentions, "code:x.ts").Edge("decision:d", EdgeType.Quotes, "record:r").Build();

        var first = MapAreas.Assign(graph);
        var again = MapAreas.Assign(graph);
        var reversed = MapAreas.Assign(new AreaGraph().Node("feature:a", "feature:b", "feature:b.c", "surface:screen/s", "code:x.ts", "record:r", "decision:d")
            .Edge("feature:b.c", EdgeType.PartOf, "feature:b").Edge("surface:screen/s", EdgeType.PartOf, "feature:b.c").Edge("code:x.ts", EdgeType.Implements, "feature:a")
            .Edge("code:x.ts", EdgeType.Implements, "surface:screen/s").Edge("record:r", EdgeType.Mentions, "code:x.ts").Edge("decision:d", EdgeType.Quotes, "record:r").Build(reversed: true));

        Assert.Equal(Flat(first), Flat(again));
        Assert.Equal(Flat(first), Flat(reversed));
        Assert.Equal("feature:a", AreaOf(first, "code:x.ts"));   // one edge to each, the smallest key wins
    }

    [Fact, Trait("Lane", "unit")]
    public void One_umbrella_feature_is_replaced_by_its_children_and_it_and_what_reaches_only_it_are_unassigned()
    {
        var graph = new AreaGraph().Node("feature:u", "feature:u.a", "feature:u.b", "feature:u.c", "surface:screen/own", "surface:screen/sa", "code:x.ts")
            .Edge("feature:u.a", EdgeType.PartOf, "feature:u").Edge("feature:u.b", EdgeType.PartOf, "feature:u").Edge("feature:u.c", EdgeType.PartOf, "feature:u")
            .Edge("surface:screen/own", EdgeType.PartOf, "feature:u").Edge("surface:screen/sa", EdgeType.PartOf, "feature:u.a").Edge("code:x.ts", EdgeType.Implements, "surface:screen/sa")
            .Build(padded: false);

        var index = MapAreas.Assign(graph);

        Assert.Equal("feature:u.a feature:u.b feature:u.c", string.Join(' ', index.ByNode.Values.Select(v => v.Area).Where(a => a != AreaIndex.UnassignedKey).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));
        Assert.Equal(("feature:u.a", "feature:u.a"), (AreaOf(index, "surface:screen/sa"), AreaOf(index, "code:x.ts")));
        Assert.All(_umbrella, k => Assert.Equal(AreaIndex.UnassignedKey, AreaOf(index, k)));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_root_holding_over_half_the_nodes_is_replaced_by_its_children_and_the_others_stay()
    {
        var graph = new AreaGraph().Node("feature:big", "feature:big.a", "feature:big.b", "surface:screen/s1", "surface:screen/s2", "feature:x", "feature:y", "feature:z")
            .Edge("feature:big.a", EdgeType.PartOf, "feature:big").Edge("feature:big.b", EdgeType.PartOf, "feature:big")
            .Edge("surface:screen/s1", EdgeType.PartOf, "feature:big.a").Edge("surface:screen/s2", EdgeType.PartOf, "feature:big.b").Build(padded: false);

        var index = MapAreas.Assign(graph);

        Assert.Equal(("feature:big.a", "feature:big.b", "feature:x", AreaIndex.UnassignedKey), (AreaOf(index, "surface:screen/s1"), AreaOf(index, "surface:screen/s2"), AreaOf(index, "feature:x"), AreaOf(index, "feature:big")));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_area_over_the_cap_is_replaced_by_its_children_even_when_it_is_not_half_the_map()
    {
        var builder = new AreaGraph().Node("feature:a", "feature:a.c1", "feature:a.c2").Edge("feature:a.c1", EdgeType.PartOf, "feature:a").Edge("feature:a.c2", EdgeType.PartOf, "feature:a");
        for (var i = 0; i < 10; i++)
        {
            _ = builder.Node("feature:o" + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var graph = builder.Build(padded: false);

        Assert.Equal("feature:a", AreaOf(MapAreas.Assign(graph, areaCap: 3), "feature:a.c1"));
        Assert.Equal("feature:a.c1", AreaOf(MapAreas.Assign(graph, areaCap: 2), "feature:a.c1"));
    }

    [Fact, Trait("Lane", "unit")]
    public void With_fewer_than_three_roots_the_largest_that_has_children_is_replaced()
    {
        var graph = new AreaGraph().Node("feature:a", "feature:a.x", "feature:a.y", "feature:b", "feature:b.p")
            .Edge("feature:a.x", EdgeType.PartOf, "feature:a").Edge("feature:a.y", EdgeType.PartOf, "feature:a").Edge("feature:b.p", EdgeType.PartOf, "feature:b").Build(padded: false);

        var index = MapAreas.Assign(graph);

        Assert.Equal(("feature:a.x", "feature:b"), (AreaOf(index, "feature:a.x"), AreaOf(index, "feature:b.p")));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_depth_goes_down_twice_at_most_and_a_dominant_area_after_that_stays()
    {
        var graph = new AreaGraph().Node("feature:u", "feature:u.v", "feature:u.v.w", "feature:u.v.w.leaf", "surface:screen/s")
            .Edge("feature:u.v", EdgeType.PartOf, "feature:u").Edge("feature:u.v.w", EdgeType.PartOf, "feature:u.v").Edge("feature:u.v.w.leaf", EdgeType.PartOf, "feature:u.v.w")
            .Edge("surface:screen/s", EdgeType.PartOf, "feature:u.v.w.leaf").Build(padded: false);

        var index = MapAreas.Assign(graph);

        Assert.Equal(("feature:u.v.w", "feature:u.v.w", AreaIndex.UnassignedKey, AreaIndex.UnassignedKey), (AreaOf(index, "feature:u.v.w.leaf"), AreaOf(index, "surface:screen/s"), AreaOf(index, "feature:u"), AreaOf(index, "feature:u.v")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_dominant_root_with_no_children_stays()
    {
        var graph = new AreaGraph().Node("feature:a", "surface:screen/s1", "surface:screen/s2", "feature:b").Edge("surface:screen/s1", EdgeType.PartOf, "feature:a").Edge("surface:screen/s2", EdgeType.PartOf, "feature:a").Build(padded: false);

        Assert.Equal("feature:a", AreaOf(MapAreas.Assign(graph), "surface:screen/s1"));
    }

    private static string Flat(AreaIndex index) => string.Join('\n', index.ByNode.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + p.Value.Area + "+" + string.Join(',', p.Value.AlsoIn)));
}
