using System.Text.Json;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Glossary;
using Aw.Context.Core.Graph;
using Aw.Context.Tests.Glossary;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class ConceptGlossaryDeriveTests
{
    private static readonly IReadOnlyList<ConceptPin> _noPins = [];

    private static GraphNode Node(string name) => new(NodeKey.From("feature:" + name), name);

    private static GraphEdge PartOf(string child, string root) => new(NodeKey.From($"feature:{child}"), NodeKey.From($"feature:{root}"), EdgeType.PartOf);

    // Three areas of three features, and one loose feature no area takes.
    private static GraphSnapshot Product()
    {
        string[] roots = ["sched", "billing", "tabs"];
        var nodes = roots.SelectMany(r => new[] { Node(r), Node(r + ".one"), Node(r + ".two") }).Append(Node("lone")).ToList();
        var edges = roots.SelectMany(r => new[] { PartOf(r + ".one", r), PartOf(r + ".two", r) }).ToList();
        return new GraphSnapshot(nodes, edges, [], []);
    }

    // Billing and scheduling on their own axes; the tab features and the loose feature sit together on a third.
    private static MapGlossary Glossary() => GlossaryBuilder.Build(
    [
        GlossaryFixtures.Feature("feature:billing", "Invoices", ("InvoiceTotal", [1f, 0f, 0f])),
        GlossaryFixtures.Feature("feature:sched", "Shifts", ("ShiftSwap", [0f, 1f, 0f])),
        GlossaryFixtures.Feature("feature:tabs", "Tab strip", ("TabStrip", [0f, 0f, 1f])),
        GlossaryFixtures.Feature("feature:tabs.one", "Tab strip order", ("tabStripOrder", [0f, 0f, 1f])),
        GlossaryFixtures.Feature("feature:lone", "Tab drag", ("TabDrag", [0f, 0f, 1f])),
    ], new GlossaryRules());

    private static HashSet<string> Members(ExtractedMap map, string concept) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.BelongsTo && e.To.Value == concept).Select(e => e.From.Value)];

    private static GraphNode Concept(ExtractedMap map, string key) => Assert.Single(map.Nodes, n => n.Key.Value == key);

    private static List<string> Terms(GraphNode concept)
    {
        using var document = JsonDocument.Parse(concept.Attributes!.Value.GetRawText());
        return document.RootElement.TryGetProperty("terms", out var terms) ? [.. terms.EnumerateArray().Select(t => t.GetString()!)] : [];
    }

    [Fact]
    public void A_concept_carries_the_terms_of_its_features_in_its_attributes()
    {
        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, Glossary());

        var terms = Terms(Concept(map, "concept:tabs"));

        Assert.Contains("strip", terms, StringComparer.Ordinal);
        Assert.DoesNotContain("invoice", terms, StringComparer.Ordinal);
        Assert.True(terms.Count <= 6);
    }

    [Fact]
    public void Without_a_glossary_the_concepts_are_exactly_what_they_were()
    {
        var before = ConceptDeriver.Derive(Product(), _noPins, names: null);
        var after = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary: null);

        Assert.Equal(before.Nodes.Select(n => (n.Key.Value, n.Attributes!.Value.GetRawText())), after.Nodes.Select(n => (n.Key.Value, n.Attributes!.Value.GetRawText())));
        Assert.Equal(before.Edges.Select(e => (e.From.Value, e.To.Value)), after.Edges.Select(e => (e.From.Value, e.To.Value)));
        Assert.All(after.Nodes, n => Assert.Empty(Terms(n)));
    }

    [Fact]
    public void A_loose_feature_joins_the_group_whose_terms_it_shares()
    {
        var plain = ConceptDeriver.Derive(Product(), _noPins, names: null);
        var withTerms = ConceptDeriver.Derive(Product(), _noPins, names: null, Glossary());

        Assert.Contains("feature:lone", Members(plain, "concept:billing"));   // no signal at all: the tie goes to the first key
        Assert.Contains("feature:lone", Members(withTerms, "concept:tabs"));
        Assert.DoesNotContain("feature:lone", Members(withTerms, "concept:billing"));
    }

    [Fact]
    public void The_namer_is_given_the_terms_of_each_group()
    {
        var groups = ConceptDeriver.Groups(Product(), _noPins, Glossary());

        var tabs = Assert.Single(groups, g => g.Seed == "feature:tabs");
        Assert.Contains("strip", tabs.Terms, StringComparer.Ordinal);
        Assert.All(ConceptDeriver.Groups(Product(), _noPins, glossary: null), g => Assert.Empty(g.Terms));
    }

    [Fact]
    public void A_pinned_feature_stays_in_its_pin_whatever_the_glossary_says()
    {
        var pin = new ConceptPin(ConceptId.From("dragging"), "Dragging", Summary: null, ["feature:lone"], []);

        var map = ConceptDeriver.Derive(Product(), [pin], names: null, Glossary());

        Assert.Equal(["feature:lone"], Members(map, "concept:dragging"), StringComparer.Ordinal);
        Assert.DoesNotContain("feature:lone", Members(map, "concept:tabs"));
        Assert.Contains("drag", Terms(Concept(map, "concept:dragging")), StringComparer.Ordinal);
    }

    [Fact]
    public void A_feature_with_no_vectors_still_gets_a_concept_and_the_rest_group_as_before()
    {
        var glossary = GlossaryBuilder.Build([GlossaryFixtures.Feature("feature:tabs", "Tab strip", ("TabStrip", [0f, 0f, 1f])), GlossaryFixtures.Feature("feature:lone", "Loose")], new GlossaryRules());

        var map = ConceptDeriver.Derive(Product(), _noPins, names: null, glossary);

        Assert.Contains("feature:lone", Members(map, "concept:billing"));
        Assert.Equal(10, map.Edges.Where(e => e.From.Kind == NodeKind.Feature).Select(e => e.From.Value).Distinct(StringComparer.Ordinal).Count());
    }
}
