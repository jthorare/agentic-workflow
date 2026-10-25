using Aw.Context.Core.Graph;
using Aw.Kernel;

namespace Aw.Context.Features.SearchMap;

/// <summary>
/// The last few search indexes. An index is kept for the graph a caller saw and the repo cut and caller scope that asked, so a new map version, an override or a synced record (any of which makes the reader
/// hand back another graph) never serves an old index, and a build happens once per graph rather than once per question. A dropped index is released with its connection when it is collected.
/// </summary>
internal sealed class MapSearchCache
{
    private const int Kept = 4;
    private readonly List<Entry> _entries = [];
    private readonly Lock _gate = new();

    /// <summary>How many indexes were built rather than served from the cache (the tests count them).</summary>
    internal int Builds { get; private set; }

    /// <summary>The index kept for these inputs, or <paramref name="build"/>'s, kept for next time (a failed build is not kept).</summary>
    /// <param name="graph">The graph the caller saw (the reader's, before any repo cut).</param>
    /// <param name="key">The repo cut and the caller's scope.</param>
    /// <param name="build">Builds the index.</param>
    /// <returns>The index.</returns>
    internal Result<IndexedMap> Get(GraphSnapshot graph, string key, Func<Result<IndexedMap>> build)
    {
        lock (_gate)
        {
            var hit = _entries.Find(e => ReferenceEquals(e.Graph, graph) && e.Key == key);
            if (hit is not null)
            {
                _ = _entries.Remove(hit);
                _entries.Add(hit);
                return hit.Map;
            }
        }

        var built = build();
        if (built.IsError)
        {
            return built;
        }

        lock (_gate)
        {
            Builds++;
            _ = _entries.RemoveAll(e => ReferenceEquals(e.Graph, graph) && e.Key == key);
            _entries.Add(new Entry(graph, key, built.Value));
            if (_entries.Count > Kept)
            {
                _entries.RemoveAt(0);
            }
        }

        return built;
    }

    private sealed record Entry(GraphSnapshot Graph, string Key, IndexedMap Map);
}
