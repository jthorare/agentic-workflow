using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Health;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>Takes one snapshot: GitHub's pull requests, origin's main, the state disk and memory against the team's floors, and the inbox.</summary>
/// <param name="repo">The gh and git calls.</param>
/// <param name="resources">The disk and memory.</param>
/// <param name="inbox">Counts the inbox.</param>
/// <param name="values">Keeps the pull requests seen merged or closed, which the merge queue drops.</param>
/// <param name="followUps">Sweeps the worktrees of the pull requests seen to end and settles the pull requests in GitHub's merge queue.</param>
/// <param name="contexts">Measures the lanes' context windows.</param>
/// <param name="reservation">Keeps the heavy-slot reservation in step with the merge queue on every poll.</param>
internal sealed class WatchPoller(RepoCommands repo, IHostResources resources, InboxCounts inbox, IKeyValueStore values, PollFollowUps followUps, HeavyReservation reservation, LaneContextReader contexts)
{
    /// <summary>The longest one gh or git call of a poll may run.</summary>
    internal static readonly TimeSpan CallLimit = TimeSpan.FromMinutes(2);

    internal async ValueTask<Result<Polled>> Poll(TeamConfig team, bool allPrs, CancellationToken cancellationToken)
    {
        var repoDirectory = team.Repo;
        // Every call of the poll stops on the stop signal and gives up after CallLimit: a GitHub that never answers fails the poll (AW-META-012) instead of hanging the watch (#3603).
        var github = repo.Cancelling(cancellationToken).Limited(CallLimit);
        var follow = followUps.Using(github);
        var reserved = reservation.Using(github);
        var prs = await github.List(repoDirectory).ConfigureAwait(false);
        var main = prs.IsError ? Results.Failure<string>(prs.Error) : await github.RemoteMain(repoDirectory).ConfigureAwait(false);
        var disk = await resources.Read(cancellationToken).ConfigureAwait(false);
        var counts = await inbox.Read(cancellationToken).ConfigureAwait(false);
        if (FirstError(prs, main, disk, counts) is { } failed)
        {
            return failed;
        }

        var kept = allPrs ? Results.Success(prs.Value) : await follow.Owned(team, prs.Value, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        var remembered = await Remember(kept.Value, cancellationToken).ConfigureAwait(false);
        if (remembered.IsError)
        {
            return remembered.Error;
        }

        // Advisory: the queue changes with ready messages, drops and pull requests that end, so every poll follows it (and renews the time).
        _ = await reserved.Apply(team, cancellationToken).ConfigureAwait(false);
        var snapshot = new WatchSnapshot(kept.Value, main.Value, ResourceRules.DiskLow(disk.Value, team.Resources), ResourceRules.MemoryLow(disk.Value, team.Resources));
        var (worktrees, queue) = await follow.Run(team, remembered.Value, snapshot, cancellationToken).ConfigureAwait(false);
        // Advisory: a transcript that can't be read leaves the poll's context out instead of failing it.
        return new Polled(snapshot, counts.Value.Total, worktrees, await contexts.ReadOrNull(team, cancellationToken).ConfigureAwait(false)) { Open = counts.Value.Open, Status = counts.Value.Status, Queue = queue };
    }

    private static Error? FirstError(Result<IReadOnlyList<PrView>> prs, Result<string> main, Result<HostResources> disk, Result<(int Total, int Open, IReadOnlyList<(string Id, string Lane)> Status)> counts) =>
        prs.IsError ? prs.Error : main.IsError ? main.Error : disk.IsError ? disk.Error : counts.IsError ? counts.Error : null;

    // Keeps the pull requests GitHub shows merged or closed, so the merge queue can drop them; returns the ones not seen ended before.
    private async ValueTask<Result<IReadOnlyList<int>>> Remember(IReadOnlyList<PrView> prs, CancellationToken cancellationToken)
    {
        var kept = await values.Get(KvKeys.EndedPrs, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return kept.Error;
        }

        var added = EndedPrs.Added(kept.Value, prs);
        var stored = EndedPrs.Merge(kept.Value, prs) is { } ended ? await values.Set(KvKeys.EndedPrs, ended, cancellationToken).ConfigureAwait(false) : Results.Ok();
        return stored.IsError ? stored.Error : Results.Success(added);
    }
}
