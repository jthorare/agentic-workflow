using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

/// <summary>There are no built-in file roles: what a repo's 'index.roles' names never reaches clustering, and a repo that names none excludes nothing.</summary>
public sealed class SharedFileClusterTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("web-app");

    private static ClusterSet Set(params FileCluster[] clusters) => new(clusters, Available: true);

    private static FileCluster Cluster(string id, double confidence, params string[] files) => new(ClusterId.From(id), files, confidence);

    [Fact, Trait("Lane", "unit")]
    public void A_repo_with_no_roles_excludes_nothing_every_file_of_a_cluster_implements_its_feature()
    {
        string[] files =
        [
            "src/orders/place.ts",
            "src/icons/arrow.ts",
            "src/crons/nightly.ts",
            "src/api/api-router.ts",
            "src/orders/__tests__/place.test.ts",
            "src/demo/ids.ts",
        ];

        var map = ClusterFeatures.Propose(_repo, Set(Cluster("c1", 0.55, files)), [], ExtractedMap.Empty);

        var implementers = map.Edges.Where(e => e.Type == EdgeType.Implements).Select(e => e.From.Value);
        Assert.Equal(files.Select(f => "code:" + f).Order(StringComparer.Ordinal), implementers.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_file_a_role_excludes_is_filtered_before_clustering_so_it_implements_nothing()
    {
        var repo = new ContextRepo(_repo, "/work/web-app") { DenyPaths = [.. PathDenylist.Defaults, "**/crons/**"] };
        var cluster = Cluster("c1", 0.55, "src/orders/place.ts", "src/crons/nightly.ts");
        var kept = cluster with { Files = [.. cluster.Files.Where(f => !repo.Denies(f))] };

        var map = ClusterFeatures.Propose(_repo, Set(kept), [], ExtractedMap.Empty);

        Assert.Equal(["code:src/orders/place.ts"], map.Edges.Where(e => e.Type == EdgeType.Implements).Select(e => e.From.Value), StringComparer.Ordinal);
    }
}
