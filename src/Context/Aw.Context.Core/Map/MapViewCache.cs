using System.Runtime.CompilerServices;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Overrides;
using Aw.Kernel;

namespace Aw.Context.Core.Map;

/// <summary>
/// The views <see cref="MapReader"/> built, kept per stored graph: the deny cut and the overrides applied cost seconds on a 24k-node map, and every
/// lens and query read rebuilt them. A view is answered again for the same graph (the store hands back the same snapshot while nothing was written),
/// the same overrides (their count and the newest row: overrides are only ever appended), the same intent layer and the same caller and mode. One
/// per process; the graph is held weakly, so a graph the store let go of takes its views with it.
/// </summary>
public sealed class MapViewCache
{
    private readonly ConditionalWeakTable<GraphSnapshot, List<Entry>> _byGraph = [];
    private readonly Lock _gate = new();

    /// <summary>Called once per view built (the read-cost test counts them).</summary>
    internal Action? Built { get; init; }

    /// <summary>The view for these inputs: the one kept, or <paramref name="build"/>'s, kept for next time.</summary>
    internal Result<MapView> View(MapSnapshot map, IntentLayer layer, ContextScope scope, string mode, Func<Result<MapView>> build)
    {
        var key = string.Concat(mode, "\n", Caller(scope), "\n", Overrides(map.Overrides));
        var entry = Join(map, layer, key, build);
        try
        {
            return entry.View.Value;
        }
        catch
        {
            // a build that threw is not kept: the next caller builds again
            Drop(map, entry);
            throw;
        }
    }

    // The entry for these inputs: the one kept or being built (concurrent callers share one build), or a new one.
    private Entry Join(MapSnapshot map, IntentLayer layer, string key, Func<Result<MapView>> build)
    {
        lock (_gate)
        {
            var entries = _byGraph.GetOrCreateValue(map.Graph);
            if (entries.Find(e => e.Key == key && ReferenceEquals(e.Layer, layer)) is { } hit)
            {
                return hit;
            }

            _ = entries.RemoveAll(e => e.Key == key);   // a newer layer for the same caller replaces the older one
            var entry = new Entry(key, layer, new Lazy<Result<MapView>>(() => Run(build), LazyThreadSafetyMode.ExecutionAndPublication));
            entries.Add(entry);
            return entry;
        }
    }

    private Result<MapView> Run(Func<Result<MapView>> build)
    {
        var built = build();
        Built?.Invoke();
        return built;
    }

    private void Drop(MapSnapshot map, Entry entry)
    {
        lock (_gate)
        {
            if (_byGraph.TryGetValue(map.Graph, out var entries))
            {
                _ = entries.Remove(entry);
            }
        }
    }

    // Who reads and under which profile: the context section, the repos and their deny globs, the caller's kind and repo.
    private static string Caller(ContextScope scope) =>
        string.Join('\n', scope.Settings?.GetRawText() ?? string.Empty, string.Join(';', scope.Repos.Select(r => r.Name.Value + "=" + r.Path + ":" + string.Join(',', r.DenyPaths))), scope.Caller.Kind, scope.Caller.Repo?.Value ?? string.Empty);

    private static string Overrides(IReadOnlyList<MapOverride> overrides) =>
        overrides.Count == 0 ? "0" : overrides.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + overrides[^1].ToId().Value;

    private sealed record Entry(string Key, IntentLayer Layer, Lazy<Result<MapView>> View);
}
