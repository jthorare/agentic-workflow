using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

/// <summary>A product cluster whose files share no directory is keyed by where its code lives (its first sorted file's directory), never root.&lt;stem&gt;, root.&lt;stem&gt;-N or the bare stem (#1302).</summary>
public sealed class NoSharedDirectoryKeyTests
{
    private static readonly ContextRepo _monorepo = new(ContextRepoName.From("monorepo"), "/work/monorepo");

    private static ClusterSet Set(IEnumerable<string[]> clusters) =>
        new([.. clusters.Select((files, i) => new FileCluster(ClusterId.From($"c{i.ToString(CultureInfo.InvariantCulture)}"), files, 0.55))], Available: true);

    private static ExtractedMap Propose(IEnumerable<string[]> clusters) => ClusterFeatures.Propose(_monorepo, Set(clusters), [], ExtractedMap.Empty);

    private static string KeyOf(ExtractedMap map, string file) =>
        map.Edges.Single(e => e.Type == EdgeType.Implements && e.From.Value == "code:" + file).To.Value;

    [Fact, Trait("Lane", "unit")]
    public void Clusters_with_no_shared_directory_build_no_root_or_bare_stem_key()
    {
        var map = Propose(
        [
            ["apps/a/index.ts", "packages/b/index.ts"],
            ["apps/c/index.ts", "packages/d/index.ts"],
            ["apps/e/index.ts", "packages/f/index.ts"],
        ]);

        var features = map.Nodes.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value).ToList();
        Assert.Equal(3, features.Count);
        Assert.DoesNotContain(features, k => k.StartsWith("feature:root.", StringComparison.Ordinal));
        Assert.DoesNotContain(features, k => string.Equals(k, "feature:index", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_with_no_shared_directory_is_keyed_by_its_first_files_directory()
    {
        var map = Propose([["apps/a/index.ts", "packages/b/index.ts"]]);

        Assert.Equal("feature:apps.a", KeyOf(map, "apps/a/index.ts"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_cluster_sharing_a_directory_is_still_keyed_by_that_directory()
    {
        var map = Propose([["apps/web/src/orders/place.ts", "apps/web/src/orders/cancel.ts"]]);

        Assert.Equal("feature:apps.web.src.orders", KeyOf(map, "apps/web/src/orders/place.ts"));
    }
}
