using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class ConceptDeriverTests
{
    private static readonly IReadOnlyList<ConceptPin> _noPins = [];
    private static readonly string[] _threeRoots = ["sched", "billing", "tabs"];

    private static GraphNode Feature(string name, string? label = null) => new(NodeKey.From("feature:" + name), label ?? name);

    private static GraphEdge Edge(string from, string to, EdgeType type) => new(NodeKey.From(from), NodeKey.From(to), type);

    // A root with two children: three features that the area rules group together.
    private static (List<GraphNode> Nodes, List<GraphEdge> Edges) Area(string root, string? label = null) =>
        ([Feature(root, label), Feature(root + ".one"), Feature(root + ".two")],
         [Edge($"feature:{root}.one", $"feature:{root}", EdgeType.PartOf), Edge($"feature:{root}.two", $"feature:{root}", EdgeType.PartOf)]);

    private static GraphSnapshot Graph(IEnumerable<GraphNode> nodes, IEnumerable<GraphEdge> edges) => new([.. nodes], [.. edges], [], []);

    private static GraphSnapshot Areas(params string[] roots)
    {
        var parts = roots.Select(r => Area(r)).ToList();
        return Graph(parts.SelectMany(p => p.Nodes), parts.SelectMany(p => p.Edges));
    }

    private static HashSet<string> Members(ExtractedMap map, string concept) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.To.Value == concept).Select(e => e.From.Value)];

    private static GraphNode Concept(ExtractedMap map, string key) => Assert.Single(map.Nodes, n => n.Key.Value == key);

    private static ConceptNames Named(string seed, string name, string? summary = null) =>
        new(new Dictionary<string, ConceptName>(StringComparer.Ordinal) { [seed] = new ConceptName(name, summary) });

    [Fact]
    public void Groups_come_from_the_part_of_roots()
    {
        var map = ConceptDeriver.Derive(Areas("sched", "billing", "tabs"), _noPins, names: null);

        Assert.Equal(["concept:billing", "concept:other", "concept:sched", "concept:tabs"], map.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["feature:sched", "feature:sched.one", "feature:sched.two"], Members(map, "concept:sched").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.All(map.Edges, e => Assert.Equal(EdgeType.BelongsTo, e.Type));
    }

    [Fact]
    public void The_smallest_groups_merge_until_there_are_at_most_fifteen()
    {
        var roots = Enumerable.Range(0, 20).Select(i => "area" + i.ToString("D2", CultureInfo.InvariantCulture)).ToArray();

        var map = ConceptDeriver.Derive(Areas(roots), _noPins, names: null);

        var concepts = map.Nodes.Where(n => n.Key.Value != ConceptKeys.Other).ToList();
        Assert.True(concepts.Count <= 15);
        var features = map.Edges.Where(e => e.From.Kind == NodeKind.Feature).GroupBy(e => e.From.Value, StringComparer.Ordinal).ToList();
        Assert.Equal(60, features.Count);
        Assert.All(features, f => Assert.Single(f));
    }

    [Fact]
    public void A_group_under_three_features_merges_into_the_group_it_is_most_connected_to()
    {
        var parts = _threeRoots.Select(r => Area(r)).ToList();
        var nodes = parts.SelectMany(p => p.Nodes).Concat([Feature("lone"), Feature("other-lone")]);
        var edges = parts.SelectMany(p => p.Edges).Concat([Edge("feature:lone", "feature:billing.one", EdgeType.DependsOn), Edge("feature:lone", "feature:billing.two", EdgeType.DependsOn)]);

        var map = ConceptDeriver.Derive(Graph(nodes, edges), _noPins, names: null);

        Assert.Contains("feature:lone", Members(map, "concept:billing"));
        Assert.DoesNotContain(map.Nodes, n => string.Equals(n.Key.Value, "concept:lone", StringComparison.Ordinal) || string.Equals(n.Key.Value, "concept:other-lone", StringComparison.Ordinal));
        Assert.Equal(11, map.Edges.Where(e => e.From.Kind == NodeKind.Feature).Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_map_with_fewer_than_five_features_gets_one_concept_per_feature()
    {
        var graph = Graph([Feature("a"), Feature("b"), Feature("c"), Feature("d")], []);

        var map = ConceptDeriver.Derive(graph, _noPins, names: null);

        Assert.Equal(["concept:a", "concept:b", "concept:c", "concept:d", "concept:other"], map.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["feature:b"], Members(map, "concept:b"));
    }

    [Fact]
    public void A_concept_keeps_its_id_when_the_model_names_it_differently()
    {
        var graph = Areas("sched", "billing", "tabs");

        var first = ConceptDeriver.Derive(graph, _noPins, Named("feature:sched", "Self scheduling"));
        var second = ConceptDeriver.Derive(graph, _noPins, Named("feature:sched", "Shift planning"));

        Assert.Equal(first.Nodes.Select(n => n.Key.Value), second.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.Equal("Self scheduling", Concept(first, "concept:sched").Label);
        Assert.Equal("Shift planning", Concept(second, "concept:sched").Label);
        Assert.Equal(Provenance.Model, Concept(first, "concept:sched").Provenance);
    }

    [Fact]
    public void Records_and_decisions_join_through_mentions_and_decides_and_an_unlinked_one_goes_to_other()
    {
        var graph = Areas("sched", "billing", "tabs");
        graph = graph with
        {
            Nodes = [.. graph.Nodes, new GraphNode(NodeKey.From("record:linear:issue/1"), "Shift swaps"), new GraphNode(NodeKey.From("record:linear:issue/2"), "Unrelated"), new GraphNode(NodeKey.From("decision:d1"), "Use weekly view")],
            Edges = [.. graph.Edges, Edge("record:linear:issue/1", "feature:sched.one", EdgeType.Mentions), Edge("decision:d1", "feature:billing.two", EdgeType.Decides)],
        };

        var map = ConceptDeriver.Derive(graph, _noPins, names: null);

        Assert.Contains("record:linear:issue/1", Members(map, "concept:sched"));
        Assert.Contains("decision:d1", Members(map, "concept:billing"));
        Assert.Equal(["record:linear:issue/2"], Members(map, ConceptKeys.Other));
        Assert.Contains(Concept(map, "concept:sched").EvidenceOrEmpty, e => e.Key.Value == "linear:issue/1");
    }

    [Fact]
    public void Pins_take_their_members_first_and_keep_their_id_and_name()
    {
        var graph = Areas("sched", "billing", "tabs");
        graph = graph with
        {
            Nodes = [.. graph.Nodes, new GraphNode(NodeKey.From("code:src/pay/charge.cs"), "charge.cs")],
            Edges = [.. graph.Edges, Edge("code:src/pay/charge.cs", "feature:tabs.two", EdgeType.Implements)],
        };
        ConceptPin[] pins =
        [
            new(ConceptId.From("scheduling"), "Self scheduling", "People pick their shifts.", ["feature:sched", "feature:sched.*"], []),
            new(Id: null, "Payments", Summary: null, [], ["src/pay/**"]),
            new(ConceptId.From("empty-one"), "Nothing yet", Summary: null, ["feature:missing"], []),
        ];

        var map = ConceptDeriver.Derive(graph, pins, names: null);

        var scheduling = Concept(map, "concept:scheduling");
        Assert.Equal(("Self scheduling", Provenance.Human), (scheduling.Label, scheduling.Provenance));
        Assert.Equal(["feature:sched", "feature:sched.one", "feature:sched.two"], Members(map, "concept:scheduling").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["feature:tabs.two"], Members(map, "concept:payments"));
        Assert.DoesNotContain("feature:tabs.two", Members(map, "concept:tabs"));
        Assert.DoesNotContain(map.Nodes, n => n.Key.Value == "concept:sched");
        Assert.Empty(Members(map, "concept:empty-one"));
        Assert.Contains(map.Nodes, n => n.Key.Value == "concept:empty-one");
        using var attributes = JsonDocument.Parse(scheduling.Attributes!.Value.GetRawText());
        Assert.Equal("override", attributes.RootElement.GetProperty("source").GetString());
        Assert.Equal("People pick their shifts.", attributes.RootElement.GetProperty("summary").GetString());
    }

    [Fact]
    public void Without_a_model_the_name_is_the_seed_label_and_the_summary_counts_the_features()
    {
        var (nodes, edges) = Area("sched", "week-view");
        nodes[1] = Feature("sched.one", "Shift swaps");
        nodes[2] = Feature("sched.two", "Open shifts");
        var graph = Graph(nodes.Concat(Area("billing").Nodes).Concat(Area("tabs").Nodes), edges.Concat(Area("billing").Edges).Concat(Area("tabs").Edges));

        var concept = Concept(ConceptDeriver.Derive(graph, _noPins, names: null), "concept:sched");

        Assert.Equal(("Week view", Provenance.Deterministic), (concept.Label, concept.Provenance));
        using var attributes = JsonDocument.Parse(concept.Attributes!.Value.GetRawText());
        Assert.Equal("3 features: week-view, Shift swaps, Open shifts", attributes.RootElement.GetProperty("summary").GetString());
        Assert.Equal(("derived", "feature:sched"), (attributes.RootElement.GetProperty("source").GetString(), attributes.RootElement.GetProperty("seed").GetString()));
    }

    [Theory]
    [InlineData("shiftService")]
    [InlineData("src/shifts")]
    [InlineData("shift_service")]
    [InlineData("Shifts.Core")]
    public void A_name_that_looks_like_an_identifier_is_refused_for_the_fallback(string name)
    {
        var concept = Concept(ConceptDeriver.Derive(Areas("sched", "billing", "tabs"), _noPins, Named("feature:sched", name)), "concept:sched");

        Assert.Equal(("Sched", Provenance.Deterministic), (concept.Label, concept.Provenance));
    }

    [Fact]
    public void Groups_name_their_features_and_records_by_title_for_the_namer()
    {
        var graph = Areas("sched", "billing", "tabs");
        graph = graph with
        {
            Nodes = [.. graph.Nodes, new GraphNode(NodeKey.From("record:linear:issue/1"), "Shift swaps")],
            Edges = [.. graph.Edges, Edge("record:linear:issue/1", "feature:sched.one", EdgeType.Mentions)],
        };

        var group = Assert.Single(ConceptDeriver.Groups(graph, _noPins), g => g.Seed == "feature:sched");

        Assert.Equal(["sched", "sched.one", "sched.two"], group.Features);
        Assert.Equal(["Shift swaps"], group.Records);
    }

    [Fact]
    public void A_feature_key_with_accents_gets_a_plain_ascii_slug()
    {
        var map = ConceptDeriver.Derive(Graph([Feature("caf\u00e9-r\u00e9sum\u00e9")], []), _noPins, names: null);

        Assert.Contains(map.Nodes, n => n.Key.Value == "concept:cafe-resume");
    }

    [Fact]
    public void Feature_keys_with_no_latin_letters_get_distinct_valid_slugs()
    {
        var map = ConceptDeriver.Derive(Graph([Feature("\u65e5\u672c\u8a9e"), Feature("\u0434\u043e\u043c")], []), _noPins, names: null);

        var slugs = map.Nodes.Where(n => n.Key.Value != ConceptKeys.Other).Select(n => n.Key.Value["concept:".Length..]).ToList();
        Assert.Equal(2, slugs.Distinct(StringComparer.Ordinal).Count());
        Assert.All(slugs, slug => Assert.Matches("^concept-[0-9a-f]{8}$", slug));
    }
}
