using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

/// <summary>A cluster made only of manifest, lock or config files (the same file in sibling monorepo packages) is never a feature: its files keep their code nodes, and no feature or concept is keyed from a manifest stem.</summary>
public sealed class ManifestClusterTests
{
    private static readonly string[] _manifestNames = ["package.json", "package-lock.json", "yarn.lock", "pnpm-lock.yaml", "tsconfig.json"];

    private static readonly ContextRepo _monorepo = new(ContextRepoName.From("monorepo"), "/work/monorepo");

    private static readonly string[][] _clusters =
    [
        ["apps/web/src/orders/place.ts", "apps/web/src/orders/cancel.ts"],
        ["apps/web/src/cart/add.ts", "apps/web/src/cart/remove.ts"],
        ["packages/billing/src/invoice.ts", "packages/billing/src/charge.ts"],
        ["packages/ui/src/button.ts", "packages/ui/src/modal.ts"],
        ["packages/auth/src/login.ts", "packages/auth/src/session.ts"],
        ["apps/api/src/routes/health.ts", "apps/api/src/routes/users.ts"],
        ["apps/web/package.json", "packages/billing/package.json", "packages/ui/package.json"],
        ["packages/auth/package.json", "apps/api/package.json"],
        ["apps/docs/package.json", "apps/admin/package.json"],
        ["packages/core/package.json", "packages/utils/package.json"],
        ["apps/web/package-lock.json", "packages/billing/package-lock.json"],
        ["apps/web/yarn.lock", "packages/ui/yarn.lock", "apps/api/pnpm-lock.yaml"],
        ["apps/web/tsconfig.json", "packages/billing/tsconfig.json", "packages/ui/tsconfig.json"],
    ];

    private static ClusterSet Set(IEnumerable<string[]> clusters) =>
        new([.. clusters.Select((files, i) => new FileCluster(ClusterId.From($"c{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}"), files, 0.55))], Available: true);

    private static bool IsManifest(string file) => _manifestNames.Contains(file[(file.LastIndexOf('/') + 1)..], StringComparer.Ordinal);

    private static ExtractedMap Propose(IEnumerable<string[]> clusters) => ClusterFeatures.Propose(_monorepo, Set(clusters), [], ExtractedMap.Empty);

    [Fact, Trait("Lane", "unit")]
    public void Clusters_of_only_manifest_files_are_not_keyed_from_the_manifest_name()
    {
        var map = Propose(_clusters);

        Assert.DoesNotContain(map.Nodes, n => n.Key.Value == "feature:package" || n.Key.Value.StartsWith("feature:root.package", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_feature_is_implemented_only_by_manifest_lock_or_config_files()
    {
        var map = Propose(_clusters);

        var onlyManifests = map.Edges.Where(e => e.Type == EdgeType.Implements)
            .GroupBy(e => e.To.Value, StringComparer.Ordinal)
            .Where(g => g.All(e => IsManifest(e.From.Name)))
            .Select(g => g.Key);
        Assert.Empty(onlyManifests);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_manifest_files_keep_their_code_nodes()
    {
        var map = Propose(_clusters);

        var manifests = _clusters.SelectMany(c => c).Where(IsManifest).Distinct(StringComparer.Ordinal);
        Assert.All(manifests, file => Assert.Contains(map.Nodes, n => n.Key.Value == "code:" + file));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_mixed_cluster_is_keyed_and_named_from_its_product_file()
    {
        var expected = Propose([["apps/web/src/orders/place.ts"]]).Nodes.Single(n => n.Key.Kind == NodeKind.Feature);

        var map = Propose([["apps/web/package.json", "apps/web/src/orders/place.ts"]]);

        var feature = map.Nodes.Single(n => n.Key.Kind == NodeKind.Feature);
        Assert.Equal(expected.Key.Value, feature.Key.Value);
        Assert.Equal(expected.Label, feature.Label);
        Assert.Contains(map.Nodes, n => n.Key.Value == "code:apps/web/package.json");
    }

    // A key made from a package.json stem: the bare stem, its "-2" duplicate suffix, or the root-prefixed form. A source dir such as packages.auth.src is not one.
    private static bool IsManifestStem(string key, string kind) =>
        key == kind + "package" || key.StartsWith(kind + "package-", StringComparison.Ordinal) || key.StartsWith(kind + "root.package", StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void Concepts_over_a_monorepo_map_are_not_keyed_from_manifest_features()
    {
        var map = Propose(_clusters);
        var graph = new GraphSnapshot([.. map.Nodes], [.. map.Edges], [], []);

        var concepts = ConceptDeriver.Derive(graph, [], names: null);

        Assert.DoesNotContain(concepts.Nodes, n => IsManifestStem(n.Key.Value, "concept:"));
        Assert.DoesNotContain(concepts.Edges, e => IsManifestStem(e.From.Value, "feature:"));
    }
}
