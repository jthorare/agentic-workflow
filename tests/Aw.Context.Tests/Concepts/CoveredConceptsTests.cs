using Aw.Context.Core.Concepts;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Core.Graph;
using GraphConceptKeys = Aw.Context.Core.Graph.ConceptKeys;
using SynthesisMember = Aw.Context.Core.ConceptSynthesis.ConceptMember;

namespace Aw.Context.Tests.Concepts;

[Trait("Lane", "unit")]
public sealed class CoveredConceptsTests
{
    private const double TwoThirds = 2.0 / 3.0;
    private static readonly string[] _features = ["feature:a", "feature:b", "feature:c"];
    private static readonly DateTimeOffset _at = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    private static SynthesizedConcept Done(string key) =>
        new(key, key, "", [new SynthesisMember("github:pr/acme/web/1", "1", "github")], "hash", SynthesisState.Done, 1, _at, _at);

    // A derived bucket of three features and a synthesized concept holding the first 'held' of them.
    private static ConceptGraph Bucket(int held, string bucketSource = "derived")
    {
        var graph = new ConceptGraph()
            .Concept("concept:aw-context", "Aw context", source: bucketSource)
            .Concept("concept:record-sync", "Record sync", source: "synthesized")
            .Node("feature:a").Node("feature:b").Node("feature:c")
            .Member("feature:a", "concept:aw-context").Member("feature:b", "concept:aw-context").Member("feature:c", "concept:aw-context");
        foreach (var feature in _features.Take(held))
        {
            _ = graph.Member(feature, "concept:record-sync");
        }

        return graph;
    }

    private static bool Has(GraphSnapshot graph, string key) => graph.Nodes.Any(n => n.Key.Value == key);

    [Fact]
    public void A_bucket_with_two_of_its_three_features_in_one_synthesized_concept_is_dropped_with_its_edges()
    {
        var fold = CoveredConcepts.Fold(Bucket(held: 2).Node("record:note").Member("record:note", "concept:aw-context").Build(), [Done("record-sync")], TwoThirds);

        Assert.Equal(1, fold.Covered);
        Assert.False(Has(fold.Graph, "concept:aw-context"));
        Assert.DoesNotContain(fold.Graph.Edges, e => e.To.Value == "concept:aw-context" || e.From.Value == "concept:aw-context");
        Assert.Equal(["feature:a", "feature:b"], fold.Graph.Edges.Where(e => e.To.Value == "concept:record-sync").Select(e => e.From.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.True(Has(fold.Graph, "feature:c"));
        Assert.True(Has(fold.Graph, "record:note"));
    }

    [Fact]
    public void A_bucket_with_one_of_three_features_covered_stays()
    {
        var graph = Bucket(held: 1).Build();

        var fold = CoveredConcepts.Fold(graph, [Done("record-sync")], TwoThirds);

        Assert.Equal((0, graph), (fold.Covered, fold.Graph));
    }

    [Fact]
    public void A_higher_share_keeps_a_bucket_two_thirds_covered()
    {
        var fold = CoveredConcepts.Fold(Bucket(held: 2).Build(), [Done("record-sync")], 0.9);

        Assert.Equal(0, fold.Covered);
        Assert.True(Has(fold.Graph, "concept:aw-context"));
    }

    [Fact]
    public void A_pin_is_never_dropped_even_when_fully_covered()
    {
        var fold = CoveredConcepts.Fold(Bucket(held: 3, bucketSource: "override").Build(), [Done("record-sync")], TwoThirds);

        Assert.Equal(0, fold.Covered);
        Assert.True(Has(fold.Graph, "concept:aw-context"));
    }

    [Fact]
    public void Features_split_across_two_synthesized_concepts_do_not_cover_a_bucket()
    {
        var graph = Bucket(held: 1).Concept("concept:retry", "Retry", source: "synthesized").Member("feature:b", "concept:retry").Build();

        var fold = CoveredConcepts.Fold(graph, [Done("record-sync"), Done("retry")], TwoThirds);

        Assert.Equal(0, fold.Covered);
    }

    [Fact]
    public void A_synthesized_concept_still_queued_covers_nothing()
    {
        var queued = Done("record-sync") with { State = SynthesisState.Queued };

        var fold = CoveredConcepts.Fold(Bucket(held: 3).Build(), [queued], TwoThirds);

        Assert.Equal(0, fold.Covered);
    }

    [Fact]
    public void A_derived_concept_that_shares_a_synthesized_key_and_the_catch_all_are_kept()
    {
        var graph = Bucket(held: 3)
            .Concept("concept:aw-context-merged", "Merged", source: "derived").Member("feature:a", "concept:aw-context-merged")
            .Concept(GraphConceptKeys.Other, "Other", source: "derived").Member("feature:a", GraphConceptKeys.Other)
            .Build();

        var fold = CoveredConcepts.Fold(graph, [Done("record-sync"), Done("aw-context-merged")], TwoThirds);

        Assert.Equal(1, fold.Covered);
        Assert.True(Has(fold.Graph, "concept:aw-context-merged"));
        Assert.True(Has(fold.Graph, GraphConceptKeys.Other));
    }

    [Fact]
    public void A_bucket_with_no_features_stays()
    {
        var graph = new ConceptGraph().Concept("concept:empty", "Empty").Concept("concept:record-sync", "Record sync", source: "synthesized").Build();

        Assert.Equal(0, CoveredConcepts.Fold(graph, [Done("record-sync")], TwoThirds).Covered);
    }
}
