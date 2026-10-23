using Aw.Context.Core.Graph;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;

namespace Aw.Context.Features.GetWholeMap;

/// <summary>
/// The last few whole-map answers (Part D, §2.3). An answer is kept for the graph a caller saw and the question and caller scope that asked it, so a new map version, an
/// override or a synced record (any of which makes the reader hand back another graph) never serves an old answer, and a session's fenced labels are never served to a human.
/// </summary>
internal sealed class WholeMapCache
{
    private const int Kept = 4;
    private readonly List<Entry> _entries = [];
    private readonly Lock _gate = new();

    /// <summary>How many answers were built rather than served from the cache (the tests count them).</summary>
    internal int Builds { get; private set; }

    /// <summary>The answer kept for these inputs, or <paramref name="build"/>'s, kept for next time.</summary>
    /// <param name="graph">The graph the caller saw (the reader's, before any repo cut).</param>
    /// <param name="key">The question and the caller's scope.</param>
    /// <param name="build">Builds the answer.</param>
    /// <returns>The answer.</returns>
    internal Result<WholeMapView> Get(GraphSnapshot graph, string key, Func<Result<WholeMapView>> build)
    {
        lock (_gate)
        {
            var hit = _entries.Find(e => ReferenceEquals(e.Graph, graph) && e.Key == key);
            if (hit is not null)
            {
                _ = _entries.Remove(hit);
                _entries.Add(hit);
                return hit.View;
            }
        }

        var view = build();
        lock (_gate)
        {
            Builds++;
            _ = _entries.RemoveAll(e => ReferenceEquals(e.Graph, graph) && e.Key == key);
            _entries.Add(new Entry(graph, key, view));
            if (_entries.Count > Kept)
            {
                _entries.RemoveAt(0);
            }
        }

        return view;
    }

    private sealed record Entry(GraphSnapshot Graph, string Key, Result<WholeMapView> View);
}
