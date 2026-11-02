using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.BuildMap;

/// <summary>The doc records of a map build (#1373, spec §11.2): one 'record' node for every doc of the repo, linked by a rule or not.</summary>
internal static class DocRecords
{
    /// <summary>Finds the repo's docs among its paths.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="paths">Every path of the repo at the commit, '/'-separated.</param>
    /// <param name="docGlobs">'context.docs.globs'.</param>
    /// <param name="directoryNames">'context.docs.directoryNames': file names that match in any directory.</param>
    /// <param name="at">The head commit's time.</param>
    /// <returns>The nodes, in path order.</returns>
    internal static IReadOnlyList<GraphNode> Find(ContextRepoName repo, IReadOnlyList<string> paths, IReadOnlyList<string> docGlobs, IReadOnlyList<string> directoryNames, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(docGlobs);
        ArgumentNullException.ThrowIfNull(directoryNames);
        return [.. paths
            .Distinct(StringComparer.Ordinal)
            .Where(p => IsDoc(p, docGlobs, directoryNames))
            .Order(StringComparer.Ordinal)
            .Select(p => Node(repo, p, at))
            .OfType<GraphNode>()];
    }

    /// <summary>The record node of a doc, or null when its path does not fit a key.</summary>
    /// <param name="repo">The repo name.</param>
    /// <param name="path">The doc's repo-relative path.</param>
    /// <returns>The node's key.</returns>
    internal static NodeKey? Key(ContextRepoName repo, string path) =>
        RepoDocs.TryKey(repo, path, out var key) && NodeKey.TryFrom("record:" + key.Value, out var node) ? node : null;

    private static bool IsDoc(string path, IReadOnlyList<string> docGlobs, IReadOnlyList<string> directoryNames) =>
        directoryNames.Contains(path[(path.LastIndexOf('/') + 1)..], StringComparer.Ordinal) || PathDenylist.MatchesExactCase(path, docGlobs);

    private static GraphNode? Node(ContextRepoName repo, string path, DateTimeOffset at) =>
        Key(repo, path) is { } key ? new GraphNode(key, path, Provenance.Deterministic, At: at) : null;
}
