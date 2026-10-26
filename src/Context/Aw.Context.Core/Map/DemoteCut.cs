using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>
/// What the repos' demote globs take out of a map a build keeps (#1043): the 'implements' edges whose file is demoted, whatever accepted them, and the accepts of those edges (a person's or the engine's), which a build undoes
/// with them. The demoted code node stays (it still shows in lenses, last); only its claim to implement a feature goes, as a deny glob cuts nodes.
/// </summary>
internal static class DemoteCut
{
    /// <summary>Drops the demoted files' 'implements' edges and writes the undo rows for the accepts standing on them.</summary>
    /// <param name="graph">The graph a build keeps.</param>
    /// <param name="overrides">Every override of the current map.</param>
    /// <param name="mapped">The repos the map is built from: only their demote globs judge a <c xml:lang="en">code:</c> key.</param>
    /// <param name="at">When the build runs.</param>
    /// <returns>The graph itself and no rows when no repo demotes anything or no such edge is kept.</returns>
    public static (GraphSnapshot Graph, IReadOnlyList<MapOverride> Undo) Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, IReadOnlyList<ContextRepo> mapped, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(mapped);
        bool IsDropped(GraphEdge e) => e.Type == EdgeType.Implements && DemotedCode.IsDemoted(e.From, mapped);
        var dropped = graph.Edges.Where(IsDropped).ToList();
        if (dropped.Count == 0)
        {
            return (graph, []);
        }

        var links = dropped.Select(e => LinkIds.For(e).Value).ToHashSet(StringComparer.Ordinal);
        var accepts = OverrideLog.Active(overrides).Where(o => o.Action == OverrideAction.Accept && o.Link is { } link && links.Contains(link.Value));
        return (graph with { Edges = [.. graph.Edges.Where(e => !IsDropped(e))] }, [.. accepts.Select(o => MapOverride.UndoingDemote(o, at))]);
    }
}
