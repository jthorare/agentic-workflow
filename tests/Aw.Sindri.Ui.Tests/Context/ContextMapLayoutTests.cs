using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The map's pure layout (F3 spec 1 §2): bands, rows, order, wrapping and edges, from the map-tabs golden and clones of it.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextMapLayoutTests
{
    private static JsonNode Golden() => GoldenFiles.Node("context-lenses", "map-tabs");

    private static LensesView Read(JsonNode node)
    {
        var read = AwJson.Deserialize<LensesView>(Encoding.UTF8.GetBytes(node.ToJsonString()), SindriApiClient.Json);
        Assert.True(read.IsSuccess, read.IsError ? read.Error.Message : string.Empty);
        return read.Value;
    }

    private static MapLayout Layout(Action<JsonNode>? edit = null)
    {
        var node = Golden();
        edit?.Invoke(node);
        return ContextMapLayout.Build(Read(node));
    }

    private static readonly string[] _surfacePair = ["surface:command/group-tabs", "surface:command/switch-tab"];

    private static bool IsOneOf(string value, params string[] options) => options.Contains(value, StringComparer.Ordinal);

    private static MapNodeLayout Node(MapLayout layout, string key) => layout.Nodes.Single(n => n.Key == key);

    private static JsonNode Product(string key, string kind) => JsonNode.Parse($$"""{"node":"{{key}}","label":"{{key}}","kind":"{{kind}}","provenance":"human","confidence":1,"confirmed":true,"trust":"trusted"}""")!;

    [Fact]
    public void The_world_is_1360_by_872_with_the_three_bands_at_their_places()
    {
        var layout = Layout();

        Assert.Equal(new MapRect(0, 0, 1360, 872), layout.World);
        Assert.Equal([(MapBand.Intent, 0d, 252d), (MapBand.Product, 304d, 336d), (MapBand.Code, 688d, 184d)], layout.Bands.Select(b => (b.Band, b.Top, b.Height)));
        Assert.Equal([Labels.ContextMapBandIntent, Labels.ContextMapBandProduct, Labels.ContextMapBandCode], layout.Bands.Select(b => b.Title), StringComparer.Ordinal);
    }

    [Fact]
    public void Every_served_node_is_placed_once_inside_its_band()
    {
        var golden = Golden();
        var layout = Layout();

        var bands = golden["bands"]!;
        var expected = bands["intent"]!["records"]!.AsArray().Count + bands["intent"]!["decisions"]!.AsArray().Count + bands["product"]!["own"]!.AsArray().Count + bands["code"]!["files"]!.AsArray().Count;
        Assert.Equal(expected, layout.Nodes.Count);
        Assert.Equal(layout.Nodes.Count, layout.Nodes.Select(n => n.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(layout.Nodes, n =>
        {
            var band = layout.Bands.Single(b => b.Band == n.Band);
            Assert.True(n.Bounds.Top >= band.Top && n.Bounds.Bottom <= band.Top + band.Height, n.Key);
            Assert.True(n.Bounds.Left >= 0 && n.Bounds.Right <= 1360, n.Key);
        });
    }

    [Fact]
    public void The_sizes_follow_the_kind_of_node()
    {
        var golden = Golden();
        var layout = Layout();

        var anchor = Node(layout, (string)golden["anchor"]!["key"]!);
        Assert.Equal((MapShape.Card, 280d, true), (anchor.Shape, anchor.Bounds.Width, anchor.IsAnchor));
        var decision = Node(layout, (string)golden["bands"]!["intent"]!["decisions"]![0]!["node"]!);
        Assert.Equal((MapShape.Card, 250d), (decision.Shape, decision.Bounds.Width));
        var ticket = Node(layout, (string)golden["bands"]!["intent"]!["records"]![0]!["key"]!);
        Assert.Equal((MapShape.Chip, 220d), (ticket.Shape, ticket.Bounds.Width));
        var file = Node(layout, "code:src/tabs/TabManager.ts");
        Assert.Equal((MapShape.Chip, 150d), (file.Shape, file.Bounds.Width));
        Assert.Equal("TabManager.ts", file.Label);
        Assert.Equal("src/tabs", file.Detail);
    }

    [Fact]
    public void Rows_sit_where_the_prototype_puts_them()
    {
        var layout = Layout();

        Assert.Equal(350, Node(layout, "feature:tabs.tab-manager").Bounds.Top);
        Assert.Equal(370, Node(layout, "feature:tabs.tab-groups").Bounds.Top);
        Assert.Equal(482, Node(layout, "surface:command/group-tabs").Bounds.Top);
        Assert.Equal(568, Node(layout, "action:tab-groups.group-selected").Bounds.Top);
        Assert.Equal(734, Node(layout, "code:src/tabs/TabManager.ts").Bounds.Top);
        Assert.Equal(88, layout.Nodes.Single(n => n.Kind == "record").Bounds.Top);
        Assert.Equal(52, layout.Nodes.First(n => n.Kind == "decision").Bounds.Top);
    }

    [Fact]
    public void A_row_is_ordered_by_the_mean_x_of_what_it_links_to_and_ties_by_key()
    {
        var layout = Layout();

        // The surfaces hang off tab-groups (group-tabs) and tab-manager (switch-tab); their actions follow them.
        var surfaces = layout.Nodes.Where(n => n.Kind == "surface").OrderBy(n => n.Bounds.X).Select(n => n.Key).ToList();
        double ParentX(string key) => Node(layout, key).Bounds.Center.X;
        var expected = _surfacePair.OrderBy(k => ParentX(string.Equals(k, "surface:command/group-tabs", StringComparison.Ordinal) ? "feature:tabs.tab-groups" : "feature:tabs.tab-manager")).ThenBy(k => k, StringComparer.Ordinal).ToList();
        Assert.Equal(expected, surfaces);
        Assert.Equal(109, Node(layout, surfaces[0]).Bounds.Center.X);
        Assert.Equal(109 + 190, Node(layout, surfaces[1]).Bounds.Center.X);
    }

    [Fact]
    public void More_than_seven_chips_in_a_row_wrap_to_a_second_row_86_px_lower_and_the_world_grows()
    {
        var layout = Layout(node =>
        {
            var own = node["bands"]!["product"]!["own"]!.AsArray();
            for (var i = 0; i < 8; i++)
            {
                own.Add(Product($"route:extra/r{i.ToString("00", System.Globalization.CultureInfo.InvariantCulture)}", "route"));
            }
        });

        var row = layout.Nodes.Where(n => n.Kind == "route").ToList();
        Assert.Equal(8, row.Count);
        Assert.Equal(5, row.Count(n => n.Bounds.Top == 482));   // the two linked surfaces take the first two columns, the seven columns of the row are full
        Assert.Equal(3, row.Count(n => n.Bounds.Top == 482 + 86));
        Assert.Equal(872 + 86, layout.World.Height);
        Assert.Equal(688 + 86, layout.Bands.Single(b => b.Band == MapBand.Code).Top);
        Assert.Equal(568 + 86, Node(layout, "action:tab-groups.group-selected").Bounds.Top);
        var overlaps = layout.Nodes.SelectMany(a => layout.Nodes.Where(b => string.CompareOrdinal(a.Key, b.Key) < 0 && a.Bounds.Intersects(b.Bounds)).Select(b => (a.Key, b.Key)));
        Assert.Empty(overlaps);
    }

    // A tiny answer: the anchor, four features after it, four routes and four files, the last file implementing the first feature.
    private static JsonNode Straight(bool withLink)
    {
        var node = Golden();
        node["bands"]!["intent"]!["records"]!.AsArray().Clear();
        node["bands"]!["intent"]!["decisions"]!.AsArray().Clear();
        node["conflicts"]!.AsArray().Clear();
        var product = node["bands"]!["product"]!;
        var own = product["own"]!.AsArray();
        var anchor = own.First(n => (string)n!["kind"]! == "feature")!.DeepClone();
        own.Clear();
        own.Add(anchor);
        for (var i = 0; i < 4; i++)
        {
            own.Add(Product($"feature:s.f{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}", "feature"));
            own.Add(Product($"route:s/r{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}", "route"));
        }

        product["edges"]!.AsArray().Clear();
        var anchorKey = (string)anchor["node"]!;
        var edges = node["edges"]!.AsArray();
        edges.Clear();
        var files = node["bands"]!["code"]!["files"]!.AsArray();
        var template = files[0]!.DeepClone();
        files.Clear();
        for (var i = 0; i < 4; i++)
        {
            var file = template.DeepClone();
            file["path"] = $"src/s/f{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}.ts";
            file["label"] = $"src/s/f{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}.ts";
            file["implements"] = new JsonArray();
            file["suggested"] = false;
            file["confirmed"] = true;
            file["provenance"] = "deterministic";
            files.Add(file);
            var target = i < 3 || !withLink ? anchorKey : "feature:s.f0";
            edges.Add(JsonNode.Parse($$"""{"from":"code:src/s/f{{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}}.ts","to":"{{target}}","type":"implements","provenance":"deterministic","link":"edge:s{{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}}","confirmed":true,"confidence":1}"""));
        }

        return node;
    }

    [Fact]
    public void A_row_that_a_straight_link_would_cross_shifts_by_half_a_column_so_the_link_passes_between_chips()
    {
        var plain = ContextMapLayout.Build(Read(Straight(withLink: false)));
        var shifted = ContextMapLayout.Build(Read(Straight(withLink: true)));

        var plainX = plain.Nodes.Where(n => n.Kind == "route").Select(n => n.Bounds.Center.X).Order().ToList();
        var shiftedX = shifted.Nodes.Where(n => n.Kind == "route").Select(n => n.Bounds.Center.X).Order().ToList();
        Assert.Equal([109d, 299, 489, 679], plainX);
        Assert.Equal([109d + 95, 299 + 95, 489 + 95, 679 + 95], shiftedX);
        var file = shifted.Nodes.Single(n => n.Key == "code:src/s/f3.ts").Bounds.Center.X;
        var feature = shifted.Nodes.Single(n => n.Key == "feature:s.f0").Bounds.Center.X;
        Assert.Equal(feature, file);   // the link is straight up and down...
        Assert.DoesNotContain(shifted.Nodes.Where(n => n.Kind == "route"), n => n.Bounds.Left - 8 <= feature && feature <= n.Bounds.Right + 8);   // ...and no chip stands on it
    }

    [Fact]
    public void The_edges_are_the_union_of_both_lists_without_doubles_and_a_conflict_replaces_the_pair_it_names()
    {
        var golden = Golden();
        var layout = Layout();

        var both = golden["bands"]!["product"]!["edges"]!.AsArray().Count + golden["edges"]!.AsArray().Count;
        Assert.Equal(both, layout.Edges.Count);   // one supersedes edge is replaced by the conflicts edge
        Assert.Equal(layout.Edges.Count, layout.Edges.Select(e => e.Key).Distinct(StringComparer.Ordinal).Count());
        var conflict = Assert.Single(layout.Edges, e => e.Type == "conflicts");
        Assert.True(conflict.Dashed);
        Assert.DoesNotContain(layout.Edges, e => e.Type == "supersedes");
        Assert.Equal(Labels.ContextMapEdgeWord("conflicts"), conflict.Label);
    }

    [Fact]
    public void An_edge_listed_twice_is_one_edge_and_one_to_a_node_that_is_not_there_is_dropped()
    {
        var layout = Layout(node =>
        {
            var edges = node["edges"]!.AsArray();
            edges.Add(edges[0]!.DeepClone());
            edges.Add(JsonNode.Parse("""{"from":"code:gone.ts","to":"feature:tabs.tab-manager","type":"implements","provenance":"human","link":"edge:x","confirmed":true,"confidence":1}"""));
        });

        Assert.Equal(Layout().Edges.Count, layout.Edges.Count);
    }

    [Fact]
    public void Cross_band_edges_are_always_labelled_and_in_band_ones_only_on_hover()
    {
        var layout = Layout();

        Assert.All(layout.Edges.Where(e => IsOneOf(e.Type, "implements", "decides", "mentions", "conflicts")), e => Assert.True(e.CrossesBands || e.Type == "conflicts", e.Key));
        Assert.All(layout.Edges.Where(e => IsOneOf(e.Type, "part-of", "on")), e => Assert.False(e.CrossesBands, e.Key));
        Assert.All(layout.Edges.Where(e => IsOneOf(e.Type, "implements", "decides", "mentions")), e => Assert.True(e.CrossesBands, e.Key));
    }

    [Fact]
    public void A_suggested_edge_is_dashed_and_its_pill_says_how_sure_the_tool_is()
    {
        var layout = Layout();

        var suggested = layout.Edges.Single(e => e.Link == "edge:402b8bed42f6611c");
        Assert.True(suggested.Dashed);
        Assert.Equal(Labels.ContextMapEdgeWord("implements") + " · " + Labels.ContextMapSuggestedPct(74), suggested.Label);
        Assert.All(layout.Edges.Where(e => e.Link != "edge:402b8bed42f6611c" && e.Type != "conflicts"), e => Assert.False(e.Dashed, e.Key));
    }

    [Theory]
    [InlineData("decides", "decided by")]
    [InlineData("supersedes", "decided by")]
    [InlineData("mentions", "mentioned in")]
    [InlineData("quotes", "mentioned in")]
    [InlineData("implements", "implemented in")]
    [InlineData("tests", "tested by")]
    [InlineData("part-of", "part of")]
    [InlineData("on", "happens on")]
    [InlineData("reads", "reads")]
    [InlineData("writes", "writes")]
    [InlineData("depends-on", "depends on")]
    [InlineData("step-of", "step of")]
    [InlineData("moves-to", "moves to")]
    [InlineData("a-new-served-type", "a-new-served-type")]
    public void Every_served_edge_type_has_its_plain_words_and_an_unknown_one_reads_raw(string type, string words) => Assert.Equal(words, Labels.ContextMapEdgeWord(type));

    [Fact]
    public void The_pill_sits_at_the_middle_of_the_path_and_the_ends_are_on_the_node_edges()
    {
        var layout = Layout();

        Assert.All(layout.Edges, e =>
        {
            var from = Node(layout, e.From).Bounds;
            var to = Node(layout, e.To).Bounds;
            Assert.True(OnEdge(from, e.Route.Start) || OnEdge(to, e.Route.Start), e.Key);
            Assert.True(OnEdge(from, e.Route.End) || OnEdge(to, e.Route.End), e.Key);
            Assert.Equal(e.Route.Midpoint(), e.Middle);
        });
    }

    private static bool OnEdge(MapRect r, MapPoint p) =>
        ((Math.Abs(p.Y - r.Top) < 0.01 || Math.Abs(p.Y - r.Bottom) < 0.01) && p.X >= r.Left && p.X <= r.Right)
        || ((Math.Abs(p.X - r.Left) < 0.01 || Math.Abs(p.X - r.Right) < 0.01) && p.Y >= r.Top && p.Y <= r.Bottom);

    [Fact]
    public void Band_counts_come_from_the_served_counts()
    {
        var layout = Layout();

        Assert.Equal(Labels.ContextMapMetaIntent(1, 2, 1), layout.Bands[0].Meta);
        Assert.Equal(Labels.ContextMapMetaProduct(1, 0, 6), layout.Bands[1].Meta);
        Assert.Equal(Labels.ContextMapMetaCode(2, 0, "unavailable"), layout.Bands[2].Meta);
    }

    [Fact]
    public void A_decision_in_a_conflict_says_so_and_a_suggested_file_is_a_suggestion()
    {
        var layout = Layout();

        Assert.All(layout.Nodes.Where(n => n.Kind == "decision"), n => Assert.True(n.HasConflict, n.Key));
        var suggested = Node(layout, "code:src/tabs/TabGroups.ts");
        Assert.True(suggested.IsSuggested);
        Assert.Equal("edge:402b8bed42f6611c", suggested.LinkId);
        Assert.False(Node(layout, "code:src/tabs/TabManager.ts").IsSuggested);
    }

    [Fact]
    public void The_same_answer_lays_out_the_same_way_twice()
        => Assert.Equal(Layout().Nodes.Select(n => (n.Key, n.Bounds)), Layout().Nodes.Select(n => (n.Key, n.Bounds)));
}
