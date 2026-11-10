using Aw.Meta.Core;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// One watch's claim on the shared watch keys (#2564). The newest watch owns them: it writes its pid to watch.owner when it starts,
/// and an older watch that finds another pid there, or loses a compare-and-set on the snapshot, ends as superseded.
/// </summary>
/// <param name="values">Holds watch.owner and watch.snapshot.</param>
/// <param name="repo">The repo the snapshot is keyed by.</param>
/// <param name="owner">This watch's pid.</param>
/// <param name="initial">The snapshot this watch last read or wrote (the value the next write must still find).</param>
internal sealed class WatchOwnership(IKeyValueStore values, string repo, string owner, string? initial)
{
    private string? _kept = initial;

    /// <summary>Writes this watch's pid to watch.owner. Advisory: a write that fails leaves the older watch running, as before.</summary>
    internal async ValueTask Claim(CancellationToken cancellationToken) =>
        _ = await values.Set(KvKeys.WatchOwner, owner, cancellationToken).ConfigureAwait(false);

    /// <summary>The pid of a newer watch, or null while this watch still owns watch.owner.</summary>
    internal async ValueTask<string?> NewerOwner(CancellationToken cancellationToken)
    {
        var current = await values.Get(KvKeys.WatchOwner, cancellationToken).ConfigureAwait(false);
        return current.IsSuccess && current.Value is { } pid && !string.Equals(pid, owner, StringComparison.Ordinal) ? pid : null;
    }

    /// <summary>
    /// Keeps the snapshot. When another watch wrote it since this one last did, watch.owner tells who: a different pid means a newer watch
    /// (this one is superseded); this watch's own pid, or an unreadable owner, means an older watch wrote before it saw the claim, so this watch overwrites it.
    /// </summary>
    /// <returns>Null when it kept it (or the store failed: advisory, the next run starts fresh); else the pid of the watch that superseded this one.</returns>
    internal async ValueTask<string?> KeepSnapshot(WatchState state, CancellationToken cancellationToken)
    {
        var text = WatchMemory.Format(repo, state);
        var wrote = await values.SetIf(KvKeys.WatchSnapshot, _kept, text, cancellationToken).ConfigureAwait(false);
        if (wrote.IsError)
        {
            return null;
        }

        if (wrote.Value)
        {
            _kept = text;
            return null;
        }

        var current = await values.Get(KvKeys.WatchOwner, cancellationToken).ConfigureAwait(false);
        if (current.IsSuccess && current.Value is { } pid && !string.Equals(pid, owner, StringComparison.Ordinal))
        {
            return pid;
        }

        // Still the owner: the writer was an older watch that has not seen the claim yet. It ends at its next owner check.
        _ = await values.Set(KvKeys.WatchSnapshot, text, cancellationToken).ConfigureAwait(false);
        _kept = text;
        return null;
    }
}
