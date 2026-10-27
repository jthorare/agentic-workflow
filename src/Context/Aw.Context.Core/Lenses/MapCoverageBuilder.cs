using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Lenses;

/// <summary>Counts how well the map covers the files behind a scope (scoping spec C13), from the same rollup 'context filemap' uses so the two never disagree.</summary>
internal static class MapCoverageBuilder
{
    private const string CodePrefix = "code:";

    /// <summary>The coverage of the files that implement <paramref name="own"/> nodes and of any <paramref name="codeKeys"/> given or reached: all their directories, and the repo's files under them counted by status.</summary>
    internal static MapCoverage Of(GraphSnapshot graph, IReadOnlySet<string> own, LensBasis basis, IEnumerable<string>? codeKeys = null)
    {
        var directories = graph.Edges.Where(e => e.Type == EdgeType.Implements && own.Contains(e.To.Value) && IsFile(e.From.Value)).Select(e => e.From.Value)
            .Concat((codeKeys ?? []).Where(IsFile))
            .Select(k => Directory(k[CodePrefix.Length..])).Where(d => d.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var paths = directories;
        var under = basis.Files.Where(f => paths.Exists(p => f.StartsWith(p, StringComparison.Ordinal))).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var totals = FileMapBuilder.Totals(under, FileLinks.For(graph));
        var created = basis.MapCreated ?? basis.AsOf;
        return new MapCoverage(paths, Truncated: false, totals.Files, totals.Unmapped, totals.Suggested, created, Math.Max(0, (int)(basis.AsOf - created).TotalDays));
    }

    private static bool IsFile(string key) => key.StartsWith(CodePrefix, StringComparison.Ordinal) && !key.Contains('#', StringComparison.Ordinal);

    private static string Directory(string path) => path[..(path.LastIndexOf('/') + 1)];
}
