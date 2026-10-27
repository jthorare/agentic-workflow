using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Graph;

namespace Aw.Context.Tests.Linking;

public sealed class IntentOverlayTests
{
    private static readonly RecordKey _r103 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000103");
    private static readonly IntentRecord _record = new(_r103, SourceId.From("onyx"), "2026-07-23T09:00:00+00:00", "Toggle split orientation", ["SPL-103"]);

    private static GraphSnapshot Map() => new GraphBuilder()
        .Node("feature:layout.split-pane.orientation", "Split orientation")
        .Alias("feature:layout.orientation", "feature:layout.split-pane.orientation")
        .Build();

    private static IntentLink Link(string target) => new(_r103, NodeKey.From(target), LinkRule.IssueKey, 1.0, "ab12");

    [Fact, Trait("Lane", "unit")]
    public void A_link_becomes_a_record_node_and_a_deterministic_mentions_edge_with_the_span_hash_as_evidence()
    {
        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record], [Link("feature:layout.split-pane.orientation")], [], []));

        var node = Assert.Single(result.Graph.Nodes, n => n.Key.Kind == NodeKind.Record);
        Assert.Equal("record:" + _r103.Value, node.Key.Value);
        Assert.Equal("Toggle split orientation", node.Label);
        Assert.Equal(Provenance.Deterministic, node.Provenance);
        var edge = Assert.Single(result.Graph.Edges);
        Assert.Equal(EdgeType.Mentions, edge.Type);
        Assert.Equal(node.Key, edge.From);
        Assert.Equal("feature:layout.split-pane.orientation", edge.To.Value);
        Assert.Equal(Provenance.Deterministic, edge.Provenance);
        Assert.Equal(new EvidenceRef(_r103, "ab12"), Assert.Single(edge.EvidenceOrEmpty));
        Assert.Empty(result.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_to_an_alias_follows_it_to_the_node()
    {
        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record], [Link("feature:layout.orientation")], [], []));

        Assert.Equal("feature:layout.split-pane.orientation", Assert.Single(result.Graph.Edges).To.Value);
        Assert.Empty(result.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_whose_target_resolves_to_nothing_is_kept_orphaned_and_adds_no_edge()
    {
        var gone = Link("feature:removed.long-ago");

        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record], [gone], [], []));

        Assert.Equal(gone, Assert.Single(result.Orphaned));
        Assert.Empty(result.Graph.Edges);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_made_only_by_a_name_is_a_model_provenance_mention_and_any_other_rule_makes_it_deterministic()
    {
        const string target = "feature:layout.split-pane.orientation";
        var byName = new IntentLink(_r103, NodeKey.From(target), LinkRule.Name, 0.5, "cd34");

        var suggested = IntentOverlay.Apply(Map(), new IntentLayer([_record], [byName], [], []));
        var confirmed = IntentOverlay.Apply(Map(), new IntentLayer([_record], [byName, Link(target)], [], []));

        var edge = Assert.Single(suggested.Graph.Edges);
        Assert.Equal((EdgeType.Mentions, Provenance.Model, 0.5), (edge.Type, edge.Provenance, edge.Confidence));
        Assert.Equal(Provenance.Deterministic, Assert.Single(confirmed.Graph.Edges).Provenance);
    }

    private static readonly RecordKey _r104 = RecordKey.From("linear:issue/0e5a1b00-0000-4000-8000-000000000104");
    private static readonly IntentRecord _target = new(_r104, SourceId.From("onyx"), "2026-07-22T10:20:00+00:00", "Restore pane layout", ["SPL-104"]);

    [Fact, Trait("Lane", "unit")]
    public void A_record_that_is_only_the_target_of_a_link_becomes_a_node_and_the_link_is_not_orphaned()
    {
        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record, _target], [Link("feature:layout.split-pane.orientation"), Link("record:" + _r104.Value)], [], []));

        Assert.Contains(result.Graph.Nodes, n => n.Key.Value == "record:" + _r104.Value);
        Assert.Contains(result.Graph.Edges, e => e.Type == EdgeType.Mentions && e.From.Value == "record:" + _r103.Value && e.To.Value == "record:" + _r104.Value);
        Assert.Empty(result.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_by_issue_key_resolves_when_the_target_record_arrives_in_any_order()
    {
        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record, _target], [Link("record:SPL-104")], [], []));

        Assert.Contains(result.Graph.Edges, e => e.Type == EdgeType.Mentions && e.From.Value == "record:" + _r103.Value && e.To.Value == "record:" + _r104.Value);
        Assert.Empty(result.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_link_by_issue_key_to_a_record_not_synced_yet_waits_and_is_not_counted_orphaned()
    {
        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record], [Link("record:SPL-999"), Link("feature:layout.split-pane.orientation")], [], []));

        Assert.Empty(result.Orphaned);
        Assert.DoesNotContain(result.Graph.Edges, e => e.To.Value.StartsWith("record:", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_becomes_a_node_with_quotes_and_decides_edges()
    {
        var decision = new DecisionRow(_r103, "keeps the focused pane first", "Toggling keeps focus first.", Provenance.Model, DateTimeOffset.Parse("2026-07-23T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var decides = new GraphEdge(decision.ToKey(), NodeKey.From("feature:layout.split-pane.orientation"), EdgeType.Decides, Provenance.Model, 0.9);

        var result = IntentOverlay.Apply(Map(), new IntentLayer([_record], [], [decision], [decides]));

        Assert.Contains(result.Graph.Nodes, n => n.Key.Value == decision.ToKey().Value);
        Assert.Contains(result.Graph.Edges, e => e.Type == EdgeType.Quotes && e.From.Value == decision.ToKey().Value && e.To.Value == "record:" + _r103.Value);
        Assert.Contains(result.Graph.Edges, e => e.Type == EdgeType.Decides && e.To.Value == "feature:layout.split-pane.orientation");
        Assert.Equal(decision, Assert.Single(result.Graph.Decisions));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_empty_layer_returns_the_same_graph()
    {
        var map = Map();

        var result = IntentOverlay.Apply(map, IntentLayer.Empty);

        Assert.Same(map, result.Graph);
        Assert.Empty(result.Orphaned);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rule_link_that_repeats_a_stored_mentions_edge_adds_no_second_edge()
    {
        const string target = "feature:layout.split-pane.orientation";
        var stored = new GraphBuilder()
            .Node(target, "Split orientation")
            .Node("record:" + _r103.Value, "Toggle split orientation", Provenance.Deterministic)
            .Edge("record:" + _r103.Value, EdgeType.Mentions, target, provenance: Provenance.Deterministic)
            .Build();

        var result = IntentOverlay.Apply(stored, new IntentLayer([_record], [Link(target)], [], []));

        Assert.Equal(1, result.Graph.Edges.Count(e => e.Type == EdgeType.Mentions && e.From.Value == "record:" + _r103.Value && e.To.Value == target));
    }
}
