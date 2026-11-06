using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Usage;
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
/// <param name="usage">Looks for lanes and the meta stopped at a usage limit, and resumes the idle ones.</param>
internal sealed class WatchService(IMetaConfig config, WatchPoller poller, IKeyValueStore values, IDelay delay, TimeProvider clock, UsageMonitor usage)
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
        var loop = await NewLoop(team.Value, request.Emit, cancellationToken).ConfigureAwait(false);
        for (var polls = 1; ; polls++)
        {
            await loop.Tick(cancellationToken).ConfigureAwait(false);
            var beat = await Beat(request.Interval, cancellationToken).ConfigureAwait(false);
            if (beat.IsError)
            {
                return beat.Error;
            }

            if (request.UntilMail && loop.Open > 0)
            {
                await loop.Say(string.Create(CultureInfo.InvariantCulture, $"INBOX {loop.Open} open message(s)")).ConfigureAwait(false);
                return new WatchResponse(polls, loop.Lines) { EndedOnMail = true };
            }

            if (clock.GetUtcNow() + request.Interval >= end)
            {
                await loop.Say("WATCH EXPIRING: rerun aw-meta watch").ConfigureAwait(false);
                return new WatchResponse(polls, loop.Lines);
            }

            await delay.Wait(request.Interval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<WatchLoop> NewLoop(TeamConfig team, Func<string, ValueTask> emit, CancellationToken cancellationToken)
    {
        var reported = await values.Get(KvKeys.WatchResources, cancellationToken).ConfigureAwait(false);
        var high = await values.Get(KvKeys.WatchContext, cancellationToken).ConfigureAwait(false);
        return new WatchLoop(
            team,
            poller,
            emit,
            WatchResources.Parse(reported.IsSuccess ? reported.Value : null),
            // Advisory: if this can't be kept, a re-armed watch announces a standing LOW again.
            async resources => _ = await values.Set(KvKeys.WatchResources, resources.ToString(), cancellationToken).ConfigureAwait(false),
            new ContextReported(
                ContextWatch.Parse(high.IsSuccess ? high.Value : null),
                async lanes => _ = await values.Set(KvKeys.WatchContext, ContextWatch.Format(lanes), cancellationToken).ConfigureAwait(false)),
            // Advisory: a pane or transcript that can't be read leaves usage out of the poll.
            async () => await usage.Check(team, cancellationToken).ConfigureAwait(false));
    }

    private async ValueTask<Result<Unit>> Beat(TimeSpan interval, CancellationToken cancellationToken)
    {
        var beat = await values.Set(KvKeys.WatchHeartbeat, clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
        return beat.IsError
            ? beat
            : await values.Set(KvKeys.WatchInterval, ((long)interval.TotalSeconds).ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
    }
}
