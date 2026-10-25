using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

/// <summary>A file whose role the repo demotes keeps its code node in the map, but never implements a feature and never names or keys one.</summary>
public sealed class DemotedRoleTests
{
    private static readonly ContextRepoName _name = ContextRepoName.From("web-app");

    private static readonly ContextRepo _plain = new(_name, "/work/web-app");

    private static readonly ContextRepo _demoting = new(_name, "/work/web-app") { DemotePaths = ["**/__tests__/**", "**/demo/**"] };

    private static ClusterSet Set(params string[] files) => new([new FileCluster(ClusterId.From("c1"), files, 0.55)], Available: true);

    private static IEnumerable<string> Implementers(ExtractedMap map) =>
        map.Edges.Where(e => e.Type == EdgeType.Implements).Select(e => e.From.Value);

    [Fact, Trait("Lane", "unit")]
    public void A_demoted_file_of_a_cluster_keeps_its_code_node_but_implements_nothing()
    {
        var set = Set("src/orders/place.ts", "src/orders/__tests__/place.test.ts", "src/demo/ids.ts");

        var map = ClusterFeatures.Propose(_demoting, set, [], ExtractedMap.Empty);

        Assert.Equal(["code:src/orders/place.ts"], Implementers(map), StringComparer.Ordinal);
        Assert.Contains(map.Nodes, n => n.Key.Value == "code:src/orders/__tests__/place.test.ts");
        Assert.Contains(map.Nodes, n => n.Key.Value == "code:src/demo/ids.ts");
    }

    [Fact, Trait("Lane", "unit")]
    public void A_feature_is_keyed_and_named_from_its_product_files_not_a_demoted_one()
    {
        var expected = ClusterFeatures.Propose(_plain, Set("src/orders/place.ts"), [], ExtractedMap.Empty).Nodes.Single(n => n.Key.Kind == NodeKind.Feature);

        var map = ClusterFeatures.Propose(_demoting, Set("src/__tests__/zz.test.ts", "src/orders/place.ts"), [], ExtractedMap.Empty);

        var feature = map.Nodes.Single(n => n.Key.Kind == NodeKind.Feature);
        Assert.Equal(expected.Key.Value, feature.Key.Value);
        Assert.Equal(expected.Label, feature.Label);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_of_only_demoted_files_proposes_no_feature()
    {
        var map = ClusterFeatures.Propose(_demoting, Set("src/orders/__tests__/place.test.ts", "src/demo/ids.ts"), [], ExtractedMap.Empty);

        Assert.DoesNotContain(map.Nodes, n => n.Key.Kind == NodeKind.Feature);
        Assert.Empty(Implementers(map));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_repo_that_demotes_nothing_is_unchanged()
    {
        string[] files = ["src/orders/place.ts", "src/orders/__tests__/place.test.ts", "src/demo/ids.ts"];

        var map = ClusterFeatures.Propose(_plain, Set(files), [], ExtractedMap.Empty);

        Assert.Equal(files.Select(f => "code:" + f).Order(StringComparer.Ordinal), Implementers(map).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
