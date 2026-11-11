using System.Globalization;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// Settles the pull requests merge-run handed to GitHub's merge queue (merge.queued). A pull request GitHub merged is recorded as
/// merge-run records one; one that is still open but left the queue, or was closed, is dropped with a QUEUE OUT line so the meta
/// can send the lane NOT OK. Every read that fails leaves its pull request for the next poll.
/// </summary>
/// <param name="repo">The gh calls.</param>
/// <param name="values">Holds the queued pull requests.</param>
/// <param name="gates">Stores the merge.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the merge.</param>
internal sealed class GithubQueueWatch(RepoCommands repo, IKeyValueStore values, IGateStore gates, IRunningLog log, TimeProvider clock)
{
    /// <summary>The lines this poll yields for the queued pull requests.</summary>
    internal async ValueTask<IReadOnlyList<string>> Settle(TeamConfig team, WatchSnapshot snapshot, CancellationToken cancellationToken)
    {
        var stored = await values.Get(KvKeys.MergeQueued, cancellationToken).ConfigureAwait(false);
        var lines = new List<string>();
        foreach (var pr in (stored.IsSuccess ? MergeQueued.Parse(stored.Value).Keys : []).Order())
        {
            var line = await Settle(team.Repo, pr, snapshot, cancellationToken).ConfigureAwait(false);
            if (line is { } said)
            {
                lines.AddRange(said);
            }
        }

        return lines;
    }

    // Null (or no lines) leaves the pull request queued; a line list, even an empty one, means it is settled and dropped.
    private async ValueTask<IReadOnlyList<string>?> Settle(string repoDirectory, int pr, WatchSnapshot snapshot, CancellationToken cancellationToken)
    {
        var state = await State(repoDirectory, pr, snapshot).ConfigureAwait(false);
        var number = pr.ToString(CultureInfo.InvariantCulture);
        switch (state)
        {
            case PrState.Merged:
                return await Merged(repoDirectory, pr, snapshot.MainSha, cancellationToken).ConfigureAwait(false);
            case PrState.Closed:
                return await Dropped(pr, $"QUEUE OUT #{number}: closed on GitHub; send the lane NOT OK", cancellationToken).ConfigureAwait(false);
            case PrState.Open:
                var view = await repo.QueueView(repoDirectory, pr).ConfigureAwait(false);
                return view.IsError || view.Value.InQueue
                    ? null
                    : await Dropped(pr, $"QUEUE OUT #{number}: {view.Value.RemovedReason ?? "left GitHub's merge queue"}; send the lane NOT OK", cancellationToken).ConfigureAwait(false);
            default:
                return null;
        }
    }

    // The poll's list holds the newest 50 pull requests; an older one is read on its own.
    private async ValueTask<PrState?> State(string repoDirectory, int pr, WatchSnapshot snapshot)
    {
        if (snapshot.Prs.FirstOrDefault(p => p.Number == pr) is { } listed)
        {
            return listed.State;
        }

        var details = await repo.View(repoDirectory, pr, "state").ConfigureAwait(false);
        return details.IsSuccess ? details.Value.State : null;
    }

    // The merge is recorded once: when merge-run (run again on the merged pull request) already did, the entry is only dropped.
    private async ValueTask<IReadOnlyList<string>?> Merged(string repoDirectory, int pr, string mainSha, CancellationToken cancellationToken)
    {
        var known = await gates.FindMerge(pr, cancellationToken).ConfigureAwait(false);
        if (known.IsError)
        {
            return null;
        }

        if (known.Value is not null)
        {
            return await Dropped(pr, line: null, cancellationToken).ConfigureAwait(false);
        }

        var now = clock.GetUtcNow();
        var record = new MergeRecord(pr, mainSha.Length > 0 ? mainSha : MergeRecord.Unknown, now);
        if ((await gates.RecordMerge(record, cancellationToken).ConfigureAwait(false)).IsError)
        {
            return null;
        }

        // Advisory: the title is for the log line only.
        var details = await repo.View(repoDirectory, pr, "title").ConfigureAwait(false);
        var title = details.IsSuccess ? details.Value.Title : string.Empty;
        var main = record.IsKnown ? record.ShortSha : "unknown";
        var number = pr.ToString(CultureInfo.InvariantCulture);
        _ = await log.Append(LogLine.Event(LogKind.Merge, now, $"#{number} {title} merged; main is now {main}"), cancellationToken).ConfigureAwait(false);
        return await Dropped(pr, $"MERGED #{number} from GitHub's merge queue: main is now {main}", cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<string>?> Dropped(int pr, string? line, CancellationToken cancellationToken)
    {
        var stored = await values.Get(KvKeys.MergeQueued, cancellationToken).ConfigureAwait(false);
        if (stored.IsError || (await values.Set(KvKeys.MergeQueued, MergeQueued.Without(stored.Value, pr), cancellationToken).ConfigureAwait(false)).IsError)
        {
            return null;
        }

        return line is null ? [] : [line];
    }
}
