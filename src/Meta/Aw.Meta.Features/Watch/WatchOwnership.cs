using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// One watch's claim on the shared watch keys (#2564). The newest watch owns them: it writes its pid to watch.owner when it starts,
/// and an older watch that finds another pid there, or loses a compare-and-set on watch.snapshot or watch.failing, ends as superseded.
/// </summary>
/// <param name="values">Holds watch.owner, watch.snapshot and watch.failing.</param>
/// <param name="repo">The repo the snapshot is keyed by.</param>
/// <param name="owner">This watch's pid.</param>
/// <param name="snapshot">The snapshot this watch read at its start (the value its first write must still find).</param>
/// <param name="failing">The failing flag this watch read at its start (the value its first write must still find).</param>
internal sealed class WatchOwnership(IKeyValueStore values, string repo, string owner, string? snapshot, string? failing)
{
    private string? _snapshot = snapshot;
    private string? _failing = failing;

    /// <summary>Writes this watch's pid to watch.owner. Advisory: a write that fails leaves the older watch running, as before.</summary>
    internal async ValueTask Claim(CancellationToken cancellationToken) =>
        _ = await values.Set(KvKeys.WatchOwner, owner, cancellationToken).ConfigureAwait(false);

    /// <summary>The pid of a newer watch, or null while this watch still owns watch.owner.</summary>
    internal async ValueTask<string?> NewerOwner(CancellationToken cancellationToken)
    {
        var current = await values.Get(KvKeys.WatchOwner, cancellationToken).ConfigureAwait(false);
        return current.IsSuccess && current.Value is { } pid && !string.Equals(pid, owner, StringComparison.Ordinal) ? pid : null;
    }

    /// <summary>Keeps the snapshot (see <see cref="Keep"/>).</summary>
    /// <returns>Null when it kept it; else the pid of the watch that superseded this one.</returns>
    internal async ValueTask<string?> KeepSnapshot(WatchState state, CancellationToken cancellationToken)
    {
        var text = WatchMemory.Format(repo, state);
        var newer = await Keep(KvKeys.WatchSnapshot, _snapshot, text, cancellationToken).ConfigureAwait(false);
        _snapshot = newer is null ? text : _snapshot;
        return newer;
    }

    /// <summary>Keeps whether polls are failing (see <see cref="Keep"/>).</summary>
    /// <returns>Null when it kept it; else the pid of the watch that superseded this one.</returns>
    internal async ValueTask<string?> KeepFailing(bool now, CancellationToken cancellationToken)
    {
        var text = now ? "1" : "0";
        var newer = await Keep(KvKeys.WatchFailing, _failing, text, cancellationToken).ConfigureAwait(false);
        _failing = newer is null ? text : _failing;
        return newer;
    }

    // A compare-and-set against what this watch last read or wrote. When another watch wrote the key meanwhile, watch.owner tells who:
    // a different pid is a newer watch (this one is superseded, and writes nothing); this watch's own pid, or an owner that can't be read,
    // is an older watch that wrote before it saw the claim, so this watch overwrites it. A store error is advisory, as before.
    private async ValueTask<string?> Keep(string key, string? expected, string text, CancellationToken cancellationToken)
    {
        var wrote = await values.SetIf(key, expected, text, cancellationToken).ConfigureAwait(false);
        if (wrote.IsError || wrote.Value)
        {
            return null;
        }

        if (await NewerOwner(cancellationToken).ConfigureAwait(false) is { } newer)
        {
            return newer;
        }

        // Still the owner: the writer was an older watch that has not seen the claim yet. It ends at its next owner check.
        _ = await values.Set(key, text, cancellationToken).ConfigureAwait(false);
        return null;
    }
}
