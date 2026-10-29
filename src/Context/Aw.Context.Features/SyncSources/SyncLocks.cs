using System.Collections.Concurrent;

namespace Aw.Context.Features.SyncSources;

/// <summary>The sources being synced right now: one sync per source at a time (SND-CONTEXT-056). One instance per host, so every request sees it.</summary>
internal sealed class SyncLocks
{
    private readonly ConcurrentDictionary<string, byte> _running = new(StringComparer.Ordinal);

    public bool TryEnter(string source) => _running.TryAdd(source, 0);

    public bool IsHeld(string source) => _running.ContainsKey(source);

    public void Leave(string source) => _ = _running.TryRemove(source, out _);
}
