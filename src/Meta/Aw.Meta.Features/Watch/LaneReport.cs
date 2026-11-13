using Aw.Meta.Core;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Poll;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>What the watch reports about the lanes themselves: usage limits and, with <paramref name="missing"/>, lanes that lost their session (#2856).</summary>
/// <param name="usage">Looks for lanes and the meta stopped at a usage limit, and resumes the idle ones.</param>
/// <param name="values">Keeps what LANE DOWN reported, for a re-armed watch.</param>
/// <param name="missing">Finds the lanes with no live session; null leaves LANE DOWN / LANE UP out.</param>
/// <param name="events">Receives lane.down and lane.up; null to write none.</param>
/// <param name="clock">Dates the events; the system clock when null.</param>
internal sealed class LaneReport(UsageMonitor usage, IKeyValueStore values, MissingLanes? missing = null, MetaEventSink? events = null, TimeProvider? clock = null)
{
    internal MetaEventSink? Events { get; } = events;

    internal TimeProvider Clock { get; } = clock ?? TimeProvider.System;

    internal UsageMonitor Usage { get; } = usage;

    // Advisory: lanes whose sessions can't be read are left out of the poll; if the state can't be kept, a re-armed watch reports a standing LANE DOWN again.
    internal async ValueTask<LaneSessions?> Sessions(TeamConfig team, CancellationToken cancellationToken)
    {
        if (missing is null)
        {
            return null;
        }

        var kept = await values.Get(KvKeys.WatchLanes, cancellationToken).ConfigureAwait(false);
        return new LaneSessions(
            team,
            async () => await missing.Find(team, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } found ? found.Value : null,
            async state => _ = await values.Set(KvKeys.WatchLanes, state.Format(), cancellationToken).ConfigureAwait(false),
            LaneWatch.Parse(kept.IsSuccess ? kept.Value : null))
        {
            Events = Events,
            Clock = Clock,
        };
    }
}
