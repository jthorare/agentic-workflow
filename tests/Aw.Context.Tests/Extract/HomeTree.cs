using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;

namespace Aw.Context.Tests.Extract;

/// <summary>Path lists and lookups the home feature tests share.</summary>
internal static class HomeTree
{
    internal static readonly ContextRepo Work = new(ContextRepoName.From("work"), "/work");

    internal static ContextRepo Repo(string name, string[]? demote = null, string[]? deny = null) =>
        new(ContextRepoName.From(name), "/" + name) { DemotePaths = demote ?? [], DenyPaths = deny ?? [] };

    /// <summary>'<paramref name="count"/>' files named f000.ts, f001.ts ... under <paramref name="dir"/> (no trailing slash; empty for the repo root).</summary>
    internal static IEnumerable<string> Files(string dir, int count, string prefix = "f") =>
        Enumerable.Range(0, count).Select(i => (dir.Length == 0 ? "" : dir + "/") + prefix + i.ToString("D3", CultureInfo.InvariantCulture) + ".ts");

    /// <summary>The 1,704-file package of the design note (§2.1): 18 leaves holding files, the biggest 150.</summary>
    internal static List<string> ApiPackage()
    {
        const string src = "packages/api/src";
        List<string> paths = ["packages/api/package.json"];
        (string Dir, int Count)[] dirs =
        [
            (src, 25), (src + "/config", 18), (src + "/utils", 96), (src + "/jobs", 74), (src + "/middleware", 88), (src + "/models", 140),
            (src + "/repositories/read", 130), (src + "/repositories/write", 103),
            (src + "/routes/v2", 120), (src + "/routes/v1", 60), (src + "/routes/v1/admin", 90), (src + "/routes/v1/public", 150),
            (src + "/services/billing/invoices", 140), (src + "/services/billing/payments", 120),
            (src + "/services/auth/sessions", 110), (src + "/services/auth/oauth", 70),
            (src + "/services/notify", 95), (src + "/services/search", 75),
        ];
        foreach (var (dir, count) in dirs)
        {
            paths.AddRange(Files(dir, count));
        }

        return paths;
    }

    /// <summary>The edges that give a file its home feature.</summary>
    internal static List<GraphEdge> Homes(ExtractedMap map) =>
        [.. map.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Kind == NodeKind.Code && e.To.Kind == NodeKind.Feature)];

    /// <summary>The feature a file landed in.</summary>
    internal static string HomeOf(HomeAssignment home, string path) => home.Files[path].Value;

    internal static string[] FeatureKeys(ExtractedMap map) =>
        [.. map.Nodes.Where(n => n.Key.Kind == NodeKind.Feature).Select(n => n.Key.Value).Order(StringComparer.Ordinal)];
}
