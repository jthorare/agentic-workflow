using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

public sealed class ClusterFeaturesTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static ClusterSet Set(params FileCluster[] clusters) => new(clusters, Available: true);

    private static FileCluster Cluster(string id, double confidence, params string[] files) => new(ClusterId.From(id), files, confidence);

    private static ExtractedMap Extracted() => MapExtractor.Extract(_repo,
    [
        new RepoFile("src/tabs/ids.ts", "export type CommandId = \"close-tab\";"),
        new RepoFile("src/layout/ids.ts", "export type CommandId = \"split\";"),
    ]);

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_becomes_a_model_tag_feature_with_a_model_tags_edge_per_file()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/a.ts", "src/tabs/b.ts")), [], ExtractedMap.Empty);

        var feature = Assert.Single(map.Nodes, n => n.Key.Kind == NodeKind.Feature);
        Assert.Equal((Provenance.Model, 0.8), (feature.Provenance, feature.Confidence));
        Assert.DoesNotContain(map.Edges, e => e.Type == EdgeType.Implements);
        var edges = map.Edges.Where(e => e.Type == EdgeType.Tags).ToList();
        Assert.Equal(["code:src/tabs/a.ts", "code:src/tabs/b.ts"], edges.Select(e => e.From.Value), StringComparer.Ordinal);
        Assert.All(edges, e => Assert.Equal((feature.Key, Provenance.Model, 0.8), (e.To, e.Provenance, e.Confidence)));
        Assert.Equal(["repo:aw-split-panes/src/tabs/a.ts"], edges[0].EvidenceOrEmpty.Select(r => r.Key.Value), StringComparer.Ordinal);
        Assert.Contains(map.Nodes, n => n.Key.Value == "code:src/tabs/a.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_feature_is_named_by_the_label_or_else_by_its_directory()
    {
        var labeled = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/a.ts")), [new ClusterLabel(ClusterId.From("c1"), "Tab groups")], ExtractedMap.Empty);
        var plain = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/a.ts", "src/tabs/b.ts")), [], ExtractedMap.Empty);

        Assert.Equal("Tab groups", labeled.Nodes.Single(n => n.Key.Kind == NodeKind.Feature).Label);
        Assert.Equal("Tabs", plain.Nodes.Single(n => n.Key.Kind == NodeKind.Feature).Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_features_key_comes_from_its_files_so_it_survives_a_relabel_and_a_renumbering()
    {
        var first = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/a.ts")), [new ClusterLabel(ClusterId.From("c1"), "Tab groups")], ExtractedMap.Empty);
        var second = ClusterFeatures.Propose(_repo, Set(Cluster("c9", 0.6, "src/tabs/a.ts")), [new ClusterLabel(ClusterId.From("c9"), "Grouping")], ExtractedMap.Empty);

        Assert.Equal(first.Nodes.Single(n => n.Key.Kind == NodeKind.Feature).Key, second.Nodes.Single(n => n.Key.Kind == NodeKind.Feature).Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_clusters_in_the_same_directory_get_different_keys()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/a.ts"), Cluster("c2", 0.7, "src/tabs/z.ts")), [], ExtractedMap.Empty);

        Assert.Equal(2, map.Nodes.Count(n => n.Key.Kind == NodeKind.Feature));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_claims_no_surface_the_home_feature_of_the_defining_file_does()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/ids.ts")), [], Extracted());

        Assert.DoesNotContain(map.Edges, e => e.Type == EdgeType.PartOf);
        var home = HomeFeatures.Assign(new ContextRepo(_repo, "/work/aw-split-panes"), ["src/tabs/ids.ts", "src/layout/ids.ts"], Extracted());
        var claim = Assert.Single(home.Map.Edges, e => e.Type == EdgeType.PartOf && e.From.Value == "surface:command/close-tab");
        Assert.Equal(("feature:aw-split-panes", Provenance.Deterministic), (claim.To.Value, claim.Provenance));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unavailable_set_a_cluster_without_files_and_no_clusters_propose_nothing()
    {
        Assert.Equal(ExtractedMap.Empty, ClusterFeatures.Propose(_repo, new ClusterSet([], Available: false), [], ExtractedMap.Empty));
        Assert.Equal(ExtractedMap.Empty, ClusterFeatures.Propose(_repo, Set(), [], ExtractedMap.Empty));
        Assert.Equal(ExtractedMap.Empty, ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.9)), [], ExtractedMap.Empty));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_edge_follows_the_edge_rules()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.8, "src/tabs/ids.ts", "src/tabs/b.ts")), [], Extracted());

        Assert.All(map.Edges, e => Assert.True(EdgeRules.Allows(e.Type, e.From.Kind, e.To.Kind), $"{e.From.Value} {e.Type} {e.To.Value}"));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("c3", true)]
    [InlineData("a-b_c", true)]
    [InlineData("", false)]
    [InlineData("-x", false)]
    [InlineData("has space", false)]
    public void A_cluster_id_is_a_short_slug(string value, bool valid) =>
        Assert.Equal(valid, ClusterId.TryFrom(value, out _));

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_id_round_trips_through_json_and_a_bad_one_is_a_json_error()
    {
        var json = JsonSerializer.Serialize(ClusterId.From("c3"));

        Assert.Equal("\"c3\"", json);
        Assert.Equal(ClusterId.From("c3"), JsonSerializer.Deserialize<ClusterId>(json));
        _ = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClusterId>("\"has space\""));
    }
}
