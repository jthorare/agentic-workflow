using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Lenses;

/// <summary>The pure step of the diff shape: changed files against a map, to their statuses, nodes and the anchors the cascade starts from.</summary>
public static class DiffAnchors
{
    /// <summary>
    /// Reads each changed path against the map, through the links of <paramref name="repo"/> only (a path that also exists in another mapped repo never claims that repo's nodes).
    /// Denied paths are only counted. Every visible file is listed by path (the list is paged by the caller, never cut here).
    /// </summary>
    /// <param name="changes">What the repo changed.</param>
    /// <param name="repo">The repo, with its deny globs.</param>
    /// <param name="graph">The map the caller may read.</param>
    /// <returns>The diff facts and the anchors (every claimed node, confirmed before suggested, then by key).</returns>
    public static (DiffPart Part, IReadOnlyList<GraphNode> Anchors) Build(RepoChanges changes, ContextRepo repo, GraphSnapshot graph)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(graph);
        var links = new FileLinks(graph, repo.Name);
        var ordered = changes.Files.OrderBy(f => f.Path, StringComparer.Ordinal).ToList();
        var visible = ordered.Where(f => !repo.Denies(f.Path)).Select(f => Read(f, links)).ToList();
        var counts = new DiffCounts(ordered.Count, visible.Count(f => f.Status == DiffStatus.Mapped), visible.Count(f => f.Status == DiffStatus.Suggested),
            visible.Count(f => f.Status == DiffStatus.Unmapped), ordered.Count - visible.Count);
        var claimed = visible.SelectMany(f => f.Nodes).GroupBy(n => n.Key, StringComparer.Ordinal)
            .Select(g => (g.Key, Confirmed: g.Any(n => n.Confirmed)))
            .OrderByDescending(c => c.Confirmed).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();
        var anchors = claimed.Select(c => graph.Resolve(NodeKey.From(c.Key))).OfType<GraphNode>().ToList();
        var kept = visible;
        var part = new DiffPart(repo.Name, changes.Base, changes.Head, changes.MergeBase, kept,
            [.. kept.Where(f => f.Status == DiffStatus.Unmapped).Select(f => f.Path)], counts, Truncated: false,
            claimed.Count);
        return (part, anchors);
    }

    private static DiffStatus Status(FileMapStatus status) => status switch
    {
        FileMapStatus.Mapped => DiffStatus.Mapped,
        FileMapStatus.Suggested => DiffStatus.Suggested,
        FileMapStatus.Unmapped => DiffStatus.Unmapped,
        _ => DiffStatus.Unmapped,
    };

    private static DiffFile Read(RepoChange change, FileLinks links)
    {
        var nodes = links.FeaturesOf(change.Path, confirmedOnly: false).Where(f => f.Edge == EdgeType.Implements)
            .GroupBy(f => f.Key.Value, StringComparer.Ordinal)
            .Select(g => new DiffNode(g.Key, g.Any(f => f.Confirmed)))
            .OrderByDescending(n => n.Confirmed).ThenBy(n => n.Key, StringComparer.Ordinal).ToList();
        return new DiffFile(change.Path, change.Change, Status(links.StatusOf(change.Path)), nodes);
    }
}
