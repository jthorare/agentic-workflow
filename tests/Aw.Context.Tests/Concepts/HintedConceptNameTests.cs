using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Concepts;

/// <summary>A scan hint that names a group names the concept built from it; a hint without a usable name leaves the concept's label as it is without hints (#1284).</summary>
[Trait("Lane", "unit")]
public sealed class HintedConceptNameTests
{
    private static readonly IReadOnlyList<ConceptPin> _noPins = [];
    private static readonly string[] _roots = ["sched", "billing", "tabs"];
    private static readonly string[] _hinted = ["feature:sched", "feature:sched.one", "feature:sched.two"];

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

    private static ConceptHint Hint(string name) => new(name, _hinted, [], 0.9);

    private static string LabelOf(ExtractedMap map, string feature)
    {
        var concept = Assert.Single(map.Edges, e => e.Type == EdgeType.BelongsTo && e.From.Value == feature).To.Value;
        return Assert.Single(map.Nodes, n => n.Key.Value == concept).Label;
    }

    [Fact]
    public void A_hinted_concept_shows_the_hints_name()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, [Hint("Self scheduling")]);

        Assert.Equal("Self scheduling", LabelOf(map, "feature:sched.one"));
    }

    [Fact]
    public void A_hint_name_wins_over_the_model_name()
    {
        var hints = new[] { Hint("Self scheduling") };
        var group = Assert.Single(ConceptDeriver.Groups(Product(), _noPins, glossary: null, hints), g => g.Features.Contains("sched.one", StringComparer.Ordinal));
        var names = new ConceptNames(new Dictionary<string, ConceptName>(StringComparer.Ordinal) { [group.Seed] = new ConceptName("Booking") });

        var map = ConceptDeriver.Derive(Product(), _noPins, names, glossary: null, hints);

        Assert.Equal("Self scheduling", LabelOf(map, "feature:sched.one"));
    }

    [Fact]
    public void A_blank_hint_name_falls_back_as_today()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, [Hint("  ")]);

        var label = LabelOf(map, "feature:sched.one");
        Assert.False(string.IsNullOrWhiteSpace(label));
        Assert.NotEqual("  ", label, StringComparer.Ordinal);
    }

    [Fact]
    public void A_concept_with_no_hint_is_unchanged()
    {
        var plain = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, []);
        var hinted = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null, [Hint("Self scheduling")]);

        Assert.Equal(LabelOf(plain, "feature:billing.one"), LabelOf(hinted, "feature:billing.one"), StringComparer.Ordinal);
    }
}
