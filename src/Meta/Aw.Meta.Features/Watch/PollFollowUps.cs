using Aw.Meta.Core.Config;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>What the watch does about pull requests after a poll took its snapshot: sweeps the worktrees of those that ended, and settles those in GitHub's merge queue.</summary>
/// <param name="sweeper">Sweeps the worktrees.</param>
/// <param name="queue">Settles the pull requests merge-run handed to GitHub's merge queue.</param>
internal sealed class PollFollowUps(WatchSweeper sweeper, GithubQueueWatch queue)
{
    internal async ValueTask<(IReadOnlyList<string> Worktrees, IReadOnlyList<string> Queue)> Run(TeamConfig team, IReadOnlyList<int> ended, WatchSnapshot snapshot, CancellationToken cancellationToken) =>
        (await sweeper.Sweep(ended, cancellationToken).ConfigureAwait(false), await queue.Settle(team, snapshot, cancellationToken).ConfigureAwait(false));
}
