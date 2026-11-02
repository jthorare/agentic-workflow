using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Overrides;

namespace Aw.Context.Core.Map;

/// <summary>
/// What a change of the map rules takes out of a map a build keeps (#1043): an edge whose only support is the engine's accept is decided again under the new rules. If the new build does not find it, the edge is dropped and each of those
/// accepts is undone; if it finds it again, it stays. An edge a person accepted is never touched here.
/// </summary>
internal static class RulesRedecide
{
    /// <summary>Drops the kept edges that only an engine accept holds up and the build no longer finds, and writes the undo rows for those accepts.</summary>
    /// <param name="graph">The graph a build keeps.</param>
    /// <param name="overrides">Every override of the current map.</param>
    /// <param name="found">What this build found.</param>
    /// <param name="rulesChanged">Whether the rules this build runs under differ from the ones the kept map was built under.</param>
    /// <param name="at">When the build runs.</param>
    /// <returns>The graph itself and no rows when the rules did not change or no engine-only edge is lost.</returns>
    public static (GraphSnapshot Graph, IReadOnlyList<MapOverride> Undo) Apply(GraphSnapshot graph, IReadOnlyList<MapOverride> overrides, ExtractedMap found, bool rulesChanged, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(found);
        if (!rulesChanged)
        {
            return (graph, []);
        }

        var seen = found.Edges.Select(e => (e.From.Value, e.To.Value, e.Type)).ToHashSet();
        var accepts = OverrideLog.Active(overrides).Where(o => o.Action == OverrideAction.Accept && o.Link is not null).ToLookup(o => o.Link?.Value ?? string.Empty, StringComparer.Ordinal);
        var lost = graph.Edges.Where(e => !seen.Contains((e.From.Value, e.To.Value, e.Type)) && OnlyEngine(accepts[LinkIds.For(e).Value])).ToList();
        if (lost.Count == 0)
        {
            return (graph, []);
        }

        var undo = lost.SelectMany(e => accepts[LinkIds.For(e).Value]).Select(o => MapOverride.UndoingRedecide(o, at));
        return (graph with { Edges = [.. graph.Edges.Except(lost)] }, [.. undo]);
    }

    private static bool OnlyEngine(IEnumerable<MapOverride> accepts)
    {
        var list = accepts.ToList();
        return list.Count > 0 && list.TrueForAll(o => string.Equals(o.Who, MapOverride.EngineWho, StringComparison.Ordinal));
    }
}
