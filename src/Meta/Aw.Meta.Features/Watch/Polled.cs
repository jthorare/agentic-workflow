using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>One poll's findings.</summary>
/// <param name="Snapshot">The pull requests, main's sha and the disk flag.</param>
/// <param name="Inbox">How many messages the inbox holds now.</param>
/// <param name="Worktrees">The worktree sweep lines of the pull requests this poll saw end; usually none.</param>
/// <param name="Context">The lanes' context sizes; null when they could not be read.</param>
internal sealed record Polled(WatchSnapshot Snapshot, int Inbox, IReadOnlyList<string> Worktrees, IReadOnlyList<LaneContext>? Context = null)
{
    /// <summary>How many messages are still owed a reply (what "aw-meta inbox" lists).</summary>
    internal int Open { get; init; }
}
