using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Overrides;
using Aw.Context.Core.Results;
using Aw.Context.Testing.Graph;
using Aw.Context.Tests.Extract;

namespace Aw.Context.Tests.Map;

/// <summary>Spec 2026-10-27 §2.3 and §5: a build drops the structure it generates itself and regenerates it, and carries a decision on a file link to the new 'tags' link.</summary>
public sealed class StructurePurgeTests
{
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-27T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    // The map as the build before this one stored it: a cluster feature with model 'implements' links, a person's own feature, a scoping proposal and a record.
    private static GraphBuilder Old() => new GraphBuilder()
        .Node("code:src/a.ts", provenance: Provenance.Deterministic).Node("code:src/b.ts", provenance: Provenance.Deterministic)
        .Node("feature:src", "Src", Provenance.Model, 0.7).Node("feature:mine", "Mine").Node("feature:proposed", "Proposed", Provenance.Model, 0.6)
        .Node("surface:command/go", provenance: Provenance.Deterministic)
        .Edge("code:src/a.ts", EdgeType.Implements, "feature:src", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:work/src/a.ts")
        .Edge("code:src/b.ts", EdgeType.Implements, "feature:src", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:work/src/b.ts")
        .Edge("surface:command/go", EdgeType.PartOf, "feature:src", provenance: Provenance.Model, confidence: 0.7)
        .Edge("code:src/a.ts", EdgeType.Implements, "feature:mine")
        .Edge("record:repo:work/docs/notes.md", EdgeType.Mentions, "feature:src", provenance: Provenance.Deterministic);

    // What this build finds: the home feature (same files), the cluster as a tag feature, the surface under the home.
    private static ExtractedMap Found()
    {
        var home = HomeFeatures.Assign(HomeTree.Repo("work"), ["src/a.ts", "src/b.ts"]);
        var tags = ClusterFeatures.Propose(ContextRepoName.From("work"), new ClusterSet([new FileCluster(ClusterId.From("c1"), ["src/a.ts", "src/b.ts"], 0.7)], Available: true), [], ExtractedMap.Empty);
        return home.Map.Plus(tags);
    }

    private static GraphSnapshot Join(GraphSnapshot kept, ExtractedMap found) =>
        kept with
        {
            Nodes = [.. kept.Nodes.Concat(found.Nodes).DistinctBy(n => n.Key.Value, StringComparer.Ordinal)],
            Edges = [.. kept.Edges.Concat(found.Edges).DistinctBy(e => (e.From.Value, e.To.Value, e.Type))],
        };

    private static MapOverride Decision(OverrideAction action, GraphEdge edge, string who = "joi") =>
        new(action, edge.From, who, _at.AddHours(-1), Link: LinkIds.For(edge));

    [Fact, Trait("Lane", "unit")]
    public void The_cut_drops_cluster_features_and_links_and_keeps_a_persons_feature_and_a_scoping_proposal()
    {
        var (graph, _, counts) = MapAccess.WithoutBuiltStructure(Old().Build(), [], Found(), _at);

        Assert.DoesNotContain(graph.Nodes, n => n.Key.Value == "feature:src");
        Assert.DoesNotContain(graph.Edges, e => e.To.Value == "feature:src");
        Assert.Contains(graph.Nodes, n => n.Key.Value == "feature:mine");
        Assert.Contains(graph.Nodes, n => n.Key.Value == "feature:proposed");
        Assert.Contains(graph.Edges, e => e.From.Value == "code:src/a.ts" && e.To.Value == "feature:mine" && e.Provenance == Provenance.Human);
        Assert.Equal((1, 2), (counts.Features, counts.Links));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_deterministic_home_structure_of_the_last_build_is_dropped_so_a_moved_file_never_has_two_homes()
    {
        var oldHome = HomeFeatures.Assign(HomeTree.Repo("work"), ["old/a.ts", "old/package.json", "src/b.ts"]);
        var stored = new GraphSnapshot([.. oldHome.Map.Nodes], [.. oldHome.Map.Edges], [], []);

        var (graph, _, _) = MapAccess.WithoutBuiltStructure(stored, [], HomeFeatures.Assign(HomeTree.Repo("work"), ["new/a.ts", "src/b.ts"]).Map, _at);
        var joined = Join(graph, HomeFeatures.Assign(HomeTree.Repo("work"), ["new/a.ts", "src/b.ts"]).Map);

        _ = Assert.Single(joined.Edges, e => e.Type == EdgeType.Implements && e.From.Value == "code:new/a.ts" && e.To.Kind == NodeKind.Feature);
        Assert.DoesNotContain(joined.Edges, e => e.From.Value == "code:old/a.ts" && e.Type == EdgeType.Implements);
        Assert.DoesNotContain(joined.Nodes, n => n.Key.Value == "feature:work.old");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_surface_claim_of_the_old_cluster_goes_with_it_and_a_human_edge_to_a_regenerated_feature_stays()
    {
        var stored = Old().Edge("record:repo:work/docs/notes.md", EdgeType.Mentions, "feature:work").Build();

        var (graph, _, _) = MapAccess.WithoutBuiltStructure(stored, [], Found(), _at);

        Assert.DoesNotContain(graph.Edges, e => e.From.Value == "surface:command/go" && e.To.Value == "feature:src");
        Assert.DoesNotContain(graph.Edges, e => e.From.Value == "record:repo:work/docs/notes.md" && e.To.Value == "feature:src");
        Assert.Contains(graph.Edges, e => e.From.Value == "record:repo:work/docs/notes.md" && e.To.Value == "feature:work");
    }

    [Fact, Trait("Lane", "unit")]
    public void An_accepted_old_link_is_carried_to_the_new_tags_link_and_the_old_decision_is_undone()
    {
        var stored = Old().Build();
        var oldLink = stored.Edges.Single(e => e.From.Value == "code:src/a.ts" && e.To.Value == "feature:src");
        var accept = Decision(OverrideAction.Accept, oldLink);
        var found = Found();
        var tag = found.Edges.Single(e => e.Type == EdgeType.Tags && e.From.Value == "code:src/a.ts");

        var (_, rows, counts) = MapAccess.WithoutBuiltStructure(stored, [accept], found, _at);

        Assert.Contains(rows, o => o.Action == OverrideAction.Undo && o.Undoes?.Value == accept.ToId().Value);
        var carried = Assert.Single(rows, o => o.Action == OverrideAction.Accept);
        Assert.Equal((LinkIds.For(tag).Value, "joi", accept.Target.Value), (carried.Link?.Value, carried.Who, carried.Target.Value));
        Assert.Equal(1, counts.Carried);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rejected_old_link_is_carried_the_same_way()
    {
        var stored = Old().Build();
        var oldLink = stored.Edges.Single(e => e.From.Value == "code:src/b.ts" && e.To.Value == "feature:src");
        var reject = Decision(OverrideAction.Reject, oldLink);

        var (_, rows, _) = MapAccess.WithoutBuiltStructure(stored, [reject], Found(), _at);

        _ = Assert.Single(rows, o => o.Action == OverrideAction.Reject && o.Link is not null && o.Link?.Value != reject.Link?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_decision_whose_pair_is_no_longer_proposed_is_undone_and_counted()
    {
        var stored = Old().Build();
        var oldLink = stored.Edges.Single(e => e.From.Value == "code:src/a.ts" && e.To.Value == "feature:src");
        var accept = Decision(OverrideAction.Accept, oldLink);
        var homeOnly = HomeFeatures.Assign(HomeTree.Repo("work"), ["src/a.ts", "src/b.ts"]).Map;

        var (_, rows, counts) = MapAccess.WithoutBuiltStructure(stored, [accept], homeOnly, _at);

        Assert.Equal([OverrideAction.Undo], rows.Select(o => o.Action));
        Assert.Equal((0, 1), (counts.Carried, counts.Undone));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rename_of_a_purged_feature_is_undone_and_one_of_a_feature_the_build_makes_again_keeps_applying()
    {
        var stored = Old().Node("feature:work", "Work", Provenance.Deterministic).Build();
        var gone = new MapOverride(OverrideAction.Rename, NodeKey.From("feature:src"), "joi", _at.AddHours(-1), Label: "Source");
        var kept = new MapOverride(OverrideAction.Rename, NodeKey.From("feature:work"), "joi", _at.AddHours(-1), Label: "The work");

        var (_, rows, counts) = MapAccess.WithoutBuiltStructure(stored, [gone, kept], Found(), _at);

        Assert.Equal([gone.ToId().Value], rows.Where(o => o.Action == OverrideAction.Undo).Select(o => o.Undoes!.Value.Value), StringComparer.Ordinal);
        Assert.Equal(1, counts.Undone);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_cut_keeps_the_data_things_and_the_code_reads_and_writes_edges_the_file_level_extraction_made()
    {
        var stored = Old()
            .Node("thing:orders", "orders", Provenance.Deterministic)
            .Edge("code:src/a.ts", EdgeType.Reads, "thing:orders", provenance: Provenance.Deterministic)
            .Edge("code:src/b.ts", EdgeType.Writes, "thing:orders", provenance: Provenance.Deterministic)
            .Edge("code:src/a.ts", EdgeType.DependsOn, "code:src/b.ts", provenance: Provenance.Deterministic)
            .Build();

        var (graph, _, _) = MapAccess.WithoutBuiltStructure(stored, [], Found(), _at);

        Assert.Contains(graph.Nodes, n => n.Key.Value == "thing:orders");
        Assert.Contains(graph.Edges, e => e is { Type: EdgeType.Reads } && e.From.Value == "code:src/a.ts" && e.To.Value == "thing:orders");
        Assert.Contains(graph.Edges, e => e is { Type: EdgeType.Writes } && e.From.Value == "code:src/b.ts");
        Assert.Contains(graph.Edges, e => e is { Type: EdgeType.DependsOn } && e.From.Value == "code:src/a.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void An_accepted_tags_link_whose_cluster_is_gone_stays_with_its_tag_feature_and_is_not_undone()
    {
        var stored = new GraphBuilder()
            .Node("code:src/a.ts", provenance: Provenance.Deterministic).Node("code:src/b.ts", provenance: Provenance.Deterministic)
            .Node("feature:tag:work:src", "Src", Provenance.Model, 0.7)
            .Edge("code:src/a.ts", EdgeType.Tags, "feature:tag:work:src", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:work/src/a.ts")
            .Edge("code:src/b.ts", EdgeType.Tags, "feature:tag:work:src", provenance: Provenance.Model, confidence: 0.7, evidence: "repo:work/src/b.ts")
            .Build();
        var accepted = stored.Edges.Single(e => e.From.Value == "code:src/a.ts");
        var accept = Decision(OverrideAction.Accept, accepted);
        var homeOnly = HomeFeatures.Assign(HomeTree.Repo("work"), ["src/a.ts", "src/b.ts"]).Map;

        var (graph, rows, counts) = MapAccess.WithoutBuiltStructure(stored, [accept], homeOnly, _at);

        Assert.Contains(graph.Nodes, n => n.Key.Value == "feature:tag:work:src");
        Assert.Contains(graph.Edges, e => e.From.Value == "code:src/a.ts" && e.To.Value == "feature:tag:work:src" && e.Type == EdgeType.Tags);
        Assert.DoesNotContain(graph.Edges, e => e.From.Value == "code:src/b.ts" && e.Type == EdgeType.Tags);
        Assert.Empty(rows);
        Assert.Equal(0, counts.Undone);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_second_build_over_the_first_ones_result_changes_nothing()
    {
        var stored = Old().Build();
        var oldLink = stored.Edges.Single(e => e.From.Value == "code:src/a.ts" && e.To.Value == "feature:src");
        var accept = Decision(OverrideAction.Accept, oldLink);
        var found = Found();
        var (first, rows, _) = MapAccess.WithoutBuiltStructure(stored, [accept], found, _at);
        var afterFirst = Join(first, found);
        List<MapOverride> log = [accept, .. rows];

        var (second, again, counts) = MapAccess.WithoutBuiltStructure(afterFirst, log, found, _at.AddHours(1));
        var afterSecond = Join(second, found);

        Assert.Empty(again);
        Assert.Equal(StructureCounts.None.Carried, counts.Carried);
        Assert.Equal(afterFirst.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)).Order(), afterSecond.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)).Order());
        Assert.Equal(afterFirst.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), afterSecond.Nodes.Select(n => n.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_declared_pin_edge_of_the_last_build_is_dropped_so_a_removed_entry_leaves_nothing_and_a_persons_own_edge_stays()
    {
        var old = new GraphBuilder().Node("concept:billing").Node("concept:orders").Node("concept:notify")
            .Edge("concept:billing", EdgeType.DependsOn, "concept:orders", evidence: "profile:index.concepts.billing.dependsOn")
            .Edge("concept:billing", EdgeType.DependsOn, "concept:notify");

        var (graph, _, _) = MapAccess.WithoutBuiltStructure(old.Build(), [], ExtractedMap.Empty, _at);

        Assert.DoesNotContain(graph.Edges, e => e.To.Value == "concept:orders");
        Assert.Contains(graph.Edges, e => e.To.Value == "concept:notify");
    }
}
