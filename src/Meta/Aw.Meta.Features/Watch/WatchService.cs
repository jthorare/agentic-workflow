using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// Polls GitHub, origin/main, the disk and the inbox every interval and emits one line per settled change. Every poll
/// writes the heartbeat (and the interval, so stop-check can tell a stale one). Shortly before the duration ends it asks to be re-armed.
/// </summary>
/// <param name="config">The team config (the repo).</param>
/// <param name="poller">Takes a snapshot.</param>
/// <param name="values">Keeps the heartbeat.</param>
/// <param name="delay">Waits between polls.</param>
/// <param name="clock">The current time.</param>
/// <param name="lanes">Reports on the lanes: usage limits (resuming the idle ones), and lanes that lost their session.</param>
/// <param name="board">Records main's new sha and the pull requests that merged when a poll sees main move.</param>
internal sealed class WatchService(IMetaConfig config, WatchPoller poller, IKeyValueStore values, IDelay delay, TimeProvider clock, LaneReport lanes, IBoardStore board)
    : IService<WatchRequest, WatchResponse>
{
    public async ValueTask<Result<WatchResponse>> Handle(WatchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        var end = clock.GetUtcNow() + request.Duration;
        var loop = await NewLoop(team.Value, request.Emit, request.Owner.ToString(CultureInfo.InvariantCulture), request.AllPrs, cancellationToken).ConfigureAwait(false);
        for (var polls = 1; ; polls++)
        {
            await loop.Tick(cancellationToken).ConfigureAwait(false);
            if (loop.Superseded is not null)
            {
                return new WatchResponse(polls, loop.Lines);
            }

            var beat = await Beat(request.Interval, cancellationToken).ConfigureAwait(false);
            if (beat.IsError)
            {
                return beat.Error;
            }

            if (await Ended(request, loop, polls, end).ConfigureAwait(false) is { } done)
            {
                return done;
            }

            await delay.Wait(request.Interval, cancellationToken).ConfigureAwait(false);
        }
    }

    // The response when this poll ends the watch (mail, an event, or the window running out); null to keep polling.
    private async ValueTask<WatchResponse?> Ended(WatchRequest request, WatchLoop loop, int polls, DateTimeOffset end)
    {
        if (request.UntilMail && loop.Open > 0)
        {
            await loop.Say(string.Create(CultureInfo.InvariantCulture, $"INBOX {loop.Open} open message(s)")).ConfigureAwait(false);
            return new WatchResponse(polls, loop.Lines) { EndedOnMail = true };
        }

        if (request.UntilEvent)
        {
            if (loop.Events > 0)
            {
                return new WatchResponse(polls, loop.Lines);
            }
        }
        else if (clock.GetUtcNow() + request.Interval >= end)
        {
            await loop.Say("WATCH EXPIRING: rerun aw-meta watch").ConfigureAwait(false);
            return new WatchResponse(polls, loop.Lines);
        }

        return null;
    }

    // The status batch, from what the last watch kept. Advisory: if it can't be kept, the next watch announces those status lines once more.
    private async ValueTask<StatusBatch> StatusBatch(CancellationToken cancellationToken)
    {
        var status = await values.Get(KvKeys.WatchStatus, cancellationToken).ConfigureAwait(false);
        return new StatusBatch(
            clock,
            StatusMemory.Parse(status.IsSuccess ? status.Value : null),
            async kept => _ = await values.Set(KvKeys.WatchStatus, kept.Format(), cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<WatchLoop> NewLoop(TeamConfig team, Func<string, ValueTask> emit, string owner, bool allPrs, CancellationToken cancellationToken)
    {
        var reported = await values.Get(KvKeys.WatchResources, cancellationToken).ConfigureAwait(false);
        var high = await values.Get(KvKeys.WatchContext, cancellationToken).ConfigureAwait(false);
        var snapshot = await values.Get(KvKeys.WatchSnapshot, cancellationToken).ConfigureAwait(false);
        var failing = await values.Get(KvKeys.WatchFailing, cancellationToken).ConfigureAwait(false);
        var kept = snapshot.IsSuccess ? snapshot.Value : null;
        var ownership = new WatchOwnership(values, team.Repo, owner, kept, failing.IsSuccess ? failing.Value : null);
        await ownership.Claim(cancellationToken).ConfigureAwait(false);
        var resources = WatchResources.Parse(reported.IsSuccess ? reported.Value : null);
        return new WatchLoop(
            team,
            poller,
            emit,
            resources,
            // Advisory: if this can't be kept, a re-armed watch announces a standing LOW again.
            async resources => _ = await values.Set(KvKeys.WatchResources, resources.ToString(), cancellationToken).ConfigureAwait(false),
            Reported(high, cancellationToken),
            // Advisory: a pane or transcript that can't be read leaves usage out of the poll.
            async () => await lanes.Usage.Check(team, cancellationToken).ConfigureAwait(false))
        {
            AllPrs = allPrs,
            EventSink = lanes.Events,
            Clock = clock,
            Sessions = await lanes.Sessions(team, cancellationToken).ConfigureAwait(false),
            Status = await StatusBatch(cancellationToken).ConfigureAwait(false),
            // Advisory: a board write that fails is printed as a WATCH ERROR line; the watch keeps polling and the next move fixes the board.
            // The last poll's pull requests and main, so a change between two runs is reported by the next run's first poll. Keyed by repo: another repo's snapshot is not used.
            Baseline = WatchMemory.Parse(kept, team.Repo, resources),
            // Advisory: if this can't be kept, the next run starts from a fresh baseline, as before.
            // A compare-and-set against what this watch last read or wrote: a newer watch that wrote meanwhile ends this one (#2564).
            KeepSnapshot = state => ownership.KeepSnapshot(state, cancellationToken),
            NewerOwner = () => ownership.NewerOwner(cancellationToken),
            // Advisory: if this can't be kept, the next run reports its first error again.
            Failing = failing.IsSuccess && string.Equals(failing.Value, "1", StringComparison.Ordinal),
            // A compare-and-set like the snapshot's: a newer watch that wrote the flag meanwhile ends this one (#2564).
            KeepFailing = now => ownership.KeepFailing(now, cancellationToken),
            MainMoved = async (sha, merged) => await board.RecordMain(sha, merged, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false),
            SeedMain = async sha => (await board.SeedMain(sha, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false)).Map(static _ => Unit.Value),
        };
    }

    // The lanes an earlier watch reported CONTEXT HIGH for, and how to keep them.
    private ContextReported Reported(Result<string?> high, CancellationToken cancellationToken) =>
        new(
            ContextWatch.Parse(high.IsSuccess ? high.Value : null),
            async lanes => _ = await values.Set(KvKeys.WatchContext, ContextWatch.Format(lanes), cancellationToken).ConfigureAwait(false));

    private async ValueTask<Result<Unit>> Beat(TimeSpan interval, CancellationToken cancellationToken)
    {
        var beat = await values.Set(KvKeys.WatchHeartbeat, clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return beat.IsError
            ? beat
            : await values.Set(KvKeys.WatchInterval, ((long)interval.TotalSeconds).ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }
}
