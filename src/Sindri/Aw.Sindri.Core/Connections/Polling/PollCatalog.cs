using System.Collections.Frozen;

namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>The v1 pollable events per preset (Spotify has none), as kinds: built from <see cref="PollableEvents"/>, the one list.</summary>
public static class PollCatalog
{
    private static readonly FrozenDictionary<string, EventKind[]> _byPreset = PollableEvents.Presets.ToFrozenDictionary(
        static preset => preset,
        static preset => PollableEvents.For(preset).Select(static key => new EventKind(key)).ToArray(),
        StringComparer.Ordinal);

    /// <summary>Lists the events a preset can be polled for.</summary>
    /// <param name="preset">The preset key ('github').</param>
    /// <returns>The events in catalog order; empty for a preset with none.</returns>
    public static IReadOnlyList<EventKind> For(string preset) => _byPreset.TryGetValue(preset, out var kinds) ? kinds : [];

    /// <summary>Finds an event by catalog id.</summary>
    /// <param name="key">The catalog id ('github.pr.merged').</param>
    /// <returns>The kind, or null when the catalog has no such event.</returns>
    public static EventKind? Find(string key)
    {
        foreach (var kinds in _byPreset.Values)
        {
            foreach (var kind in kinds)
            {
                if (string.Equals(kind.Key, key, StringComparison.Ordinal))
                {
                    return kind;
                }
            }
        }

        return null;
    }
}
