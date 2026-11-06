using System.Collections.Concurrent;

namespace Aw.Context.Features.SyncSources;

/// <summary>The sources being synced right now: one sync per source at a time (SND-CONTEXT-056). One instance per host, so every request sees it.</summary>
internal sealed class SyncLocks
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _exclusive = new(StringComparer.Ordinal);

    public bool TryEnter(string source) => _running.TryAdd(source, 0);

    public bool IsHeld(string source) => _running.ContainsKey(source);

    /// <summary>Marks a held source as held by a store change (a purge), not by a tick; <see cref="Leave"/> clears it.</summary>
    public void MarkExclusive(string source) => _ = _exclusive.TryAdd(source, 0);

    /// <summary>Whether the source is held by a store change (a purge) right now; a tick holding it is not.</summary>
    public bool IsExclusive(string source) => _exclusive.ContainsKey(source);

    public void Leave(string source)
    {
        _ = _exclusive.TryRemove(source, out _);
        _ = _running.TryRemove(source, out _);
    }
}
