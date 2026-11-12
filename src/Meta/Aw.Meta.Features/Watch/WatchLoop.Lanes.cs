using Aw.Meta.Core.Lanes;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

// The lanes' half of a poll: live sessions, context sizes and usage limits.
internal sealed partial class WatchLoop
{
    // The lanes' standing: live sessions, context sizes, then usage limits. None can fail the poll.
    private async ValueTask ReportLanes(IReadOnlyList<LaneContext>? contexts)
    {
        await ReportSessions().ConfigureAwait(false);
        await ReportContext(contexts).ConfigureAwait(false);
        foreach (var line in await usage().ConfigureAwait(false))
        {
            await Say(line).ConfigureAwait(false);
        }
    }

    // LANE DOWN once when a lane that had a session has none for two polls, LANE UP when it is back (LaneSessions).
    private async ValueTask ReportSessions()
    {
        foreach (var line in Sessions is null ? [] : await Sessions.Poll().ConfigureAwait(false))
        {
            await Say(line).ConfigureAwait(false);
        }
    }

    // CONTEXT HIGH (META RECYCLE for the meta lane) once per crossing, CONTEXT OK after a recycle; the lanes reported high are kept for a re-armed watch.
    private async ValueTask ReportContext(IReadOnlyList<LaneContext>? contexts)
    {
        if (contexts is null)
        {
            return;
        }

        var (lines, high) = ContextWatch.Step(_high, contexts, team.MetaLane);
        foreach (var line in lines)
        {
            await Say(line).ConfigureAwait(false);
        }

        if (!high.SetEquals(_high))
        {
            await WriteContextEvents(contexts, _high, high).ConfigureAwait(false);
            _high = high;
            await context.Keep(high).ConfigureAwait(false);
        }
    }

    private DateTimeOffset Now() => Clock.GetUtcNow();

    // One context.high per lane that crossed the mark in this poll, one context.ok per lane that dropped back; a standing state writes nothing.
    private async ValueTask WriteContextEvents(IReadOnlyList<LaneContext> contexts, IReadOnlySet<string> before, IReadOnlySet<string> after)
    {
        if (EventSink is null)
        {
            return;
        }

        foreach (var lane in contexts)
        {
            var tokens = lane.Tokens ?? 0;
            if (after.Contains(lane.Lane) && !before.Contains(lane.Lane))
            {
                await EventSink.Emit(Core.Events.MetaEvents.ContextHigh(Now(), lane.Lane, tokens), CancellationToken.None).ConfigureAwait(false);
            }
            else if (before.Contains(lane.Lane) && !after.Contains(lane.Lane))
            {
                await EventSink.Emit(Core.Events.MetaEvents.ContextOk(Now(), lane.Lane, tokens), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
