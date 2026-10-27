using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Concepts;

/// <summary>
/// The scan's concept hints shape the build's groups. The id rule: a hinted group keeps the id of the area group whose seed it holds, else of the one it holds more than half the members of
/// (the first hint by confidence takes it), else it gets an id from its first member that no group already uses; what a hint left behind from a group that lost its seed is a group of its own.
/// </summary>
[Trait("Lane", "unit")]
public sealed class ConceptHintTests
{
    private static readonly IReadOnlyList<ConceptPin> _noPins = [];
    private static readonly string[] _roots = ["sched", "billing", "tabs"];

    // Three areas of six features each: the root, and five children that are part of it.
    private static GraphSnapshot Product()
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        foreach (var root in _roots)
        {
            nodes.Add(new GraphNode(NodeKey.From("feature:" + root), root));
            foreach (var child in new[] { "one", "two", "three", "four", "five" })
            {
                nodes.Add(new GraphNode(NodeKey.From($"feature:{root}.{child}"), $"{root}.{child}"));
                edges.Add(new GraphEdge(NodeKey.From($"feature:{root}.{child}"), NodeKey.From("feature:" + root), EdgeType.PartOf));
            }
        }

        return new GraphSnapshot(nodes, edges, [], []);
    }

    private static ConceptHint Hint(string name, double confidence, params string[] members) => new(name, members, [], confidence);

    private static HashSet<string> Members(ExtractedMap map, string concept) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.To.Value == concept).Select(e => e.From.Value)];

    private static HashSet<string> Concept(ExtractedMap map, string feature) =>
        Members(map, Assert.Single(map.Edges, e => e.Type == EdgeType.BelongsTo && e.From.Value == feature).To.Value);

    [Fact]
    public void A_hinted_group_that_holds_an_areas_seed_keeps_that_areas_id()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, [Hint("Tab work", 0.9, "feature:tabs", "feature:sched.one", "feature:billing.one")]);

        Assert.Equal(["feature:billing.one", "feature:sched.one", "feature:tabs"], Members(map, "concept:tabs").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain("feature:sched.one", Members(map, "concept:sched"));
        Assert.Contains("feature:tabs.one", Members(map, "concept:tabs.one"));   // what the hint left of the tabs area lost its seed and its id
    }

    [Fact]
    public void A_hinted_group_with_more_than_half_of_an_area_keeps_that_areas_id_without_its_seed()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, [Hint("Tab work", 0.9, "feature:tabs.one", "feature:tabs.two", "feature:tabs.three", "feature:tabs.four")]);

        Assert.Equal(["feature:tabs.four", "feature:tabs.one", "feature:tabs.three", "feature:tabs.two"], Members(map, "concept:tabs").Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void A_hinted_group_with_neither_gets_an_id_of_its_own_and_the_areas_keep_theirs()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, [Hint("Mixed", 0.9, "feature:tabs.one", "feature:tabs.two", "feature:billing.one", "feature:billing.two")]);

        Assert.Equal(["feature:billing.one", "feature:billing.two", "feature:tabs.one", "feature:tabs.two"], Concept(map, "feature:tabs.one").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Contains("feature:tabs", Members(map, "concept:tabs"));
        Assert.Contains("feature:billing", Members(map, "concept:billing"));
    }

    [Fact]
    public void Two_hints_on_one_area_give_it_to_the_more_confident_and_the_other_gets_a_new_id()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null,
            [Hint("Weak", 0.4, "feature:tabs.one", "feature:tabs.two", "feature:tabs.three", "feature:tabs.four"), Hint("Strong", 0.9, "feature:tabs", "feature:tabs.five", "feature:billing.one")]);

        Assert.Equal(["feature:billing.one", "feature:tabs", "feature:tabs.five"], Members(map, "concept:tabs").Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["feature:tabs.four", "feature:tabs.one", "feature:tabs.three", "feature:tabs.two"], Concept(map, "feature:tabs.one").Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void A_pinned_feature_stays_in_its_pin_and_a_feature_the_map_lacks_is_ignored()
    {
        var pin = new ConceptPin(ConceptId.From("pinned"), "Pinned", Summary: null, ["feature:sched.one"], []);

        var map = ConceptDeriver.Derive(Product(), [pin], names: null, glossary: null, [Hint("Tab work", 0.9, "feature:tabs", "feature:sched.one", "feature:made.up", "feature:billing.one")]);

        Assert.Equal(["feature:sched.one"], Members(map, "concept:pinned"), StringComparer.Ordinal);
        Assert.Contains("feature:billing.one", Members(map, "concept:tabs"));
        Assert.DoesNotContain("feature:made.up", map.Edges.Select(e => e.From.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void No_hints_change_nothing()
    {
        var plain = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null);
        var hinted = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, []);

        Assert.Equal(plain.Edges.Select(e => (e.From.Value, e.To.Value)), hinted.Edges.Select(e => (e.From.Value, e.To.Value)));
    }

    [Fact]
    public void The_namer_is_given_the_hinted_groups()
    {
        var groups = ConceptDeriver.Groups(Product(), _noPins, glossary: null, [Hint("Tab work", 0.9, "feature:tabs", "feature:sched.one", "feature:billing.one")]);

        var tabs = Assert.Single(groups, g => g.Seed == "feature:tabs");
        Assert.Contains("sched.one", tabs.Features, StringComparer.Ordinal);
    }
}
