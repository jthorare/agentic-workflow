using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

/// <summary>Spec 2026-10-27 §2.3: a cluster's file links are 'tags', its key says so, and it claims no surface.</summary>
public sealed class ClusterTagsTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("work");

    private static ClusterSet Set(params FileCluster[] clusters) => new(clusters, Available: true);

    private static FileCluster Cluster(string id, params string[] files) => new(ClusterId.From(id), files, 0.8);

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_links_its_files_with_model_tags_edges_and_no_implements_edge()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", "src/tabs/a.ts", "src/tabs/b.ts")), [], ExtractedMap.Empty);

        var feature = Assert.Single(map.Nodes, n => n.Key.Kind == NodeKind.Feature);
        Assert.Equal(("feature:tag:work:src.tabs", Provenance.Model), (feature.Key.Value, feature.Provenance));
        Assert.DoesNotContain(map.Edges, e => e.Type == EdgeType.Implements);
        var tags = map.Edges.Where(e => e.Type == EdgeType.Tags).ToList();
        Assert.Equal(["code:src/tabs/a.ts", "code:src/tabs/b.ts"], tags.Select(e => e.From.Value), StringComparer.Ordinal);
        Assert.All(tags, e => Assert.Equal((feature.Key, Provenance.Model, 0.8), (e.To, e.Provenance, e.Confidence)));
        Assert.Equal(["repo:work/src/tabs/a.ts"], tags[0].EvidenceOrEmpty.Select(r => r.Key.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tags_follow_the_edge_rules_and_a_tag_key_never_equals_a_home_key()
    {
        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", "src/billing/a.ts")), [], ExtractedMap.Empty);
        var home = HomeFeatures.Assign(HomeTree.Repo("work"), ["src/billing/a.ts"]);

        Assert.All(map.Edges, e => Assert.True(EdgeRules.Allows(e.Type, e.From.Kind, e.To.Kind), $"{e.From.Value} {e.Type} {e.To.Value}"));
        var joined = map.Plus(home.Map);
        Assert.Equal(2, joined.Nodes.Count(n => n.Key.Kind == NodeKind.Feature));
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_repos_clusters_over_the_same_directory_stay_two_features()
    {
        var one = ClusterFeatures.Propose(ContextRepoName.From("one"), Set(Cluster("c1", "packages/api/a.ts")), [], ExtractedMap.Empty);
        var two = ClusterFeatures.Propose(ContextRepoName.From("two"), Set(Cluster("c1", "packages/api/a.ts")), [], ExtractedMap.Empty);

        var keys = one.Plus(two).Nodes.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value);
        Assert.Equal(["feature:tag:one:packages.api", "feature:tag:two:packages.api"], keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_claims_no_surface_because_the_home_feature_of_the_defining_file_does()
    {
        var extracted = MapExtractor.Extract(_repo, [new RepoFile("src/tabs/ids.ts", "export type CommandId = \"close-tab\";")]);

        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", "src/tabs/ids.ts")), [], extracted);

        Assert.DoesNotContain(map.Edges, e => e.Type == EdgeType.PartOf);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_tags_rule_joins_a_code_file_to_a_feature_and_nothing_else()
    {
        Assert.True(EdgeRules.Allows(EdgeType.Tags, NodeKind.Code, NodeKind.Feature));
        Assert.False(EdgeRules.Allows(EdgeType.Tags, NodeKind.Code, NodeKind.Surface));
        Assert.False(EdgeRules.Allows(EdgeType.Tags, NodeKind.Feature, NodeKind.Feature));
        Assert.Equal("tags", EdgeRules.Name(EdgeType.Tags));
        Assert.Equal(EdgeType.BelongsTo + 1, EdgeType.Tags);   // appended last: the store and map files key by the enum's names
    }
}
