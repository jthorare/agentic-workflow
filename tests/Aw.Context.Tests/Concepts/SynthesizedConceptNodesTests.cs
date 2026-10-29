using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using GraphConceptKeys = Aw.Context.Core.Graph.ConceptKeys;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class SynthesizedConceptNodesTests
{
    private const string Pr = "github:pr/acme/web/412";
    private const string Issue = "linear:issue/3f9c1c9e-0a54-4d6f-9c43-7a2b1d5e8f10";
    private const string Thread = "slack:thread/C0123/1730000000.000100";
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    private static SynthesizedConcept Concept(string key, string name, SynthesisState state, params string[] records) =>
        new(key, name, "Retries a failed checkout.", [.. records.Select(r => new Context.Core.ConceptSynthesis.ConceptMember(r, "1", r.Split(':')[0]))], "hash", state, 1, _at, _at);

    private static GraphNode Node(GraphSnapshot graph, string key) => Assert.Single(graph.Nodes, n => n.Key.Value == key);

    private static bool Belongs(GraphSnapshot graph, string from, string to) =>
        graph.Edges.Any(e => e.Type == EdgeType.BelongsTo && e.From.Value == from && e.To.Value == to);

    [Fact]
    public void A_done_concept_over_three_sources_becomes_one_model_node_with_three_evidence_refs()
    {
        var result = SynthesizedConceptNodes.Apply(GraphSnapshot.Empty, [Concept("checkout-retry", "Checkout retry", SynthesisState.Done, Thread, Pr, Issue)]);

        var node = Node(result, "concept:checkout-retry");
        Assert.Equal(NodeKind.Concept, node.Key.Kind);
        Assert.Equal("Checkout retry", node.Label);
        Assert.Equal(Provenance.Model, node.Provenance);
        Assert.Equal([Pr, Issue, Thread], node.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
        using var attributes = JsonDocument.Parse(node.Attributes!.Value.GetRawText());
        Assert.Equal("Retries a failed checkout.", attributes.RootElement.GetProperty("summary").GetString());
        Assert.Equal("synthesized", attributes.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public void A_queued_concept_adds_nothing()
    {
        var result = SynthesizedConceptNodes.Apply(GraphSnapshot.Empty, [Concept("checkout-retry", "Checkout retry", SynthesisState.Queued, Pr, Issue)]);

        Assert.Empty(result.Nodes);
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void A_key_match_merges_the_evidence_and_keeps_the_label_and_provenance()
    {
        var graph = new ConceptGraph().Concept("concept:checkout-retry", "Checkout", "Derived.", "derived", Pr).Build();

        var result = SynthesizedConceptNodes.Apply(graph, [Concept("checkout-retry", "Checkout retry", SynthesisState.Done, Pr, Issue)]);

        var node = Node(result, "concept:checkout-retry");
        Assert.Equal("Checkout", node.Label);
        Assert.Equal(Provenance.Deterministic, node.Provenance);
        Assert.Equal([Pr, Issue], node.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
        _ = Assert.Single(result.Nodes);
    }

    [Fact]
    public void Members_and_the_features_linked_to_them_belong_to_the_concept_and_other_features_do_not()
    {
        var graph = new ConceptGraph()
            .Node($"record:{Pr}").Node($"record:{Issue}")
            .Node("feature:checkout").Node("feature:cart").Node("feature:cited").Node("feature:lone")
            .Edge($"record:{Pr}", EdgeType.Mentions, "feature:checkout")
            .Edge("feature:cart", EdgeType.BelongsTo, $"record:{Issue}")
            .Build();
        graph = graph with { Nodes = [.. graph.Nodes.Select(n => n.Key.Value == "feature:cited" ? n with { Evidence = [new EvidenceRef(RecordKey.From(Thread))] } : n)] };

        var result = SynthesizedConceptNodes.Apply(graph, [Concept("checkout-retry", "Checkout retry", SynthesisState.Done, Pr, Issue, Thread)]);

        var concept = "concept:checkout-retry";
        Assert.True(Belongs(result, $"record:{Pr}", concept));
        Assert.True(Belongs(result, $"record:{Issue}", concept));
        Assert.False(Belongs(result, $"record:{Thread}", concept));   // no record node in the graph
        Assert.True(Belongs(result, "feature:checkout", concept));
        Assert.True(Belongs(result, "feature:cart", concept));
        Assert.True(Belongs(result, "feature:cited", concept));
        Assert.False(Belongs(result, "feature:lone", concept));
        Assert.Equal(result.Edges.Count, result.Edges.DistinctBy(e => (e.From.Value, e.To.Value, e.Type)).Count());
    }

    [Fact]
    public void A_member_that_is_not_a_record_key_is_skipped()
    {
        var result = SynthesizedConceptNodes.Apply(GraphSnapshot.Empty, [Concept("checkout-retry", "Checkout retry", SynthesisState.Done, "Not A Key", Pr)]);

        Assert.Equal([Pr], Node(result, "concept:checkout-retry").EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void The_same_input_gives_the_same_output_whatever_order_the_concepts_come_in()
    {
        var graph = new ConceptGraph().Node($"record:{Pr}").Node("feature:checkout").Edge($"record:{Pr}", EdgeType.Mentions, "feature:checkout").Build();
        var a = Concept("a-one", "A one", SynthesisState.Done, Pr, Issue);
        var b = Concept("b-two", "B two", SynthesisState.Done, Thread, Pr);

        var first = SynthesizedConceptNodes.Apply(graph, [a, b]);
        var second = SynthesizedConceptNodes.Apply(graph, [b, a]);

        Assert.Equal(first.Nodes.Select(n => n.Key.Value), second.Nodes.Select(n => n.Key.Value), StringComparer.Ordinal);
        Assert.Equal(first.Edges.Select(e => (e.From.Value, e.To.Value)), second.Edges.Select(e => (e.From.Value, e.To.Value)));
        Assert.Equal(first.Nodes.SelectMany(n => n.EvidenceOrEmpty).Select(e => e.Key.Value), second.Nodes.SelectMany(n => n.EvidenceOrEmpty).Select(e => e.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_human_node_with_the_same_key_is_left_alone_and_gets_no_edges()
    {
        var graph = new ConceptGraph().Concept("concept:checkout-retry", "Pinned", "Mine.", "override", Pr).Node($"record:{Issue}").Build();
        graph = graph with { Nodes = [.. graph.Nodes.Select(n => n.Key.Value == "concept:checkout-retry" ? n with { Provenance = Provenance.Human } : n)] };

        var result = SynthesizedConceptNodes.Apply(graph, [Concept("checkout-retry", "Checkout retry", SynthesisState.Done, Pr, Issue)]);

        var node = Node(result, "concept:checkout-retry");
        Assert.Equal("Pinned", node.Label);
        Assert.Equal([Pr], node.EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
        Assert.False(Belongs(result, $"record:{Issue}", "concept:checkout-retry"));
        Assert.Empty(result.Edges);
    }

    [Fact]
    public void A_derived_node_with_the_same_key_still_gets_the_members_edges()
    {
        var graph = new ConceptGraph().Concept("concept:checkout-retry", "Checkout", "Derived.", "derived", Pr).Node($"record:{Issue}").Build();

        var result = SynthesizedConceptNodes.Apply(graph, [Concept("checkout-retry", "Checkout retry", SynthesisState.Done, Pr, Issue)]);

        Assert.True(Belongs(result, $"record:{Issue}", "concept:checkout-retry"));
        Assert.Equal([Pr, Issue], Node(result, "concept:checkout-retry").EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void A_concept_keyed_other_adds_and_merges_nothing()
    {
        var graph = new ConceptGraph().Concept(GraphConceptKeys.Other, "Other", "Rest.", "derived", Pr).Node($"record:{Issue}").Build();

        var result = SynthesizedConceptNodes.Apply(graph, [Concept(GraphConceptKeys.Other["concept:".Length..], "Other", SynthesisState.Done, Pr, Issue)]);

        Assert.Equal([Pr], Node(result, GraphConceptKeys.Other).EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
        Assert.Empty(result.Edges);
        _ = Assert.Single(result.Nodes, n => n.Key.Kind == NodeKind.Concept);
    }

    [Fact]
    public void A_thirty_member_concept_keeps_the_first_twenty_five_evidence_refs_by_record_key()
    {
        var records = Enumerable.Range(0, 30).Select(i => "github:pr/acme/web/" + (100 + i).ToString(CultureInfo.InvariantCulture)).ToArray();

        var result = SynthesizedConceptNodes.Apply(GraphSnapshot.Empty, [Concept("big", "Big", SynthesisState.Done, [.. Enumerable.Reverse(records)])]);

        Assert.Equal(records.Order(StringComparer.Ordinal).Take(25), Node(result, "concept:big").EvidenceOrEmpty.Select(e => e.Key.Value), StringComparer.Ordinal);
    }

    [Fact]
    public void Merging_into_a_derived_node_also_stops_at_twenty_five_evidence_refs()
    {
        var records = Enumerable.Range(0, 30).Select(i => "github:pr/acme/web/" + (100 + i).ToString(CultureInfo.InvariantCulture)).ToArray();
        var graph = new ConceptGraph().Concept("concept:big", "Big", "Derived.", "derived", records[0]).Build();

        var result = SynthesizedConceptNodes.Apply(graph, [Concept("big", "Big", SynthesisState.Done, records)]);

        Assert.Equal(25, Node(result, "concept:big").EvidenceOrEmpty.Count);
    }
}
