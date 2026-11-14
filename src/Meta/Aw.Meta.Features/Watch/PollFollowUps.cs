using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>What the watch does about pull requests after a poll took its snapshot: sweeps the worktrees of those that ended, and settles those in GitHub's merge queue.</summary>
/// <param name="sweeper">Sweeps the worktrees.</param>
/// <param name="queue">Settles the pull requests merge-run handed to GitHub's merge queue.</param>
/// <param name="owners">Reads what decides which pull requests the team owns; null keeps every pull request.</param>
internal sealed class PollFollowUps(WatchSweeper sweeper, GithubQueueWatch queue, TeamPrInputs? owners = null)
{
    /// <summary>The same follow-ups over a poll's gh and git calls, bound by its limit and the stop signal.</summary>
    internal PollFollowUps Using(Core.Coordination.RepoCommands github) => new(sweeper, queue.Using(github), owners?.Using(github));

    // Only the pull requests the team owns (#2771): the watch's lines, the board and the ended-PR sweep all read this one set.
    internal async ValueTask<Result<IReadOnlyList<PrView>>> Owned(TeamConfig team, IReadOnlyList<PrView> prs, CancellationToken cancellationToken)
    {
        if (owners is null)
        {
            return Results.Success(prs);
        }

        var facts = await owners.Read(team, cancellationToken).ConfigureAwait(false);
        return facts.IsError
            ? facts.Error
            : Results.Success(TeamPrs.Owned(prs, facts.Value.Queued, facts.Value.Ready, team.Watch.PrBranchPrefixes, facts.Value.CheckedOut));
    }

    internal async ValueTask<(IReadOnlyList<string> Worktrees, IReadOnlyList<string> Queue)> Run(TeamConfig team, IReadOnlyList<int> ended, WatchSnapshot snapshot, CancellationToken cancellationToken) =>
        (await sweeper.Sweep(ended, cancellationToken).ConfigureAwait(false), await queue.Settle(team, snapshot, cancellationToken).ConfigureAwait(false));
}
