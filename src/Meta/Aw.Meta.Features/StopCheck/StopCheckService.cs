using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Obligations;

namespace Aw.Meta.Features.StopCheck;

/// <summary>
/// The Stop hook's question. Any session but the registered meta may stop. For the meta: sync the transcript, refresh the lane
/// map, evaluate what is owed, and block while anything is open, except that the identical open set blocks at most
/// MaxBlocks times in a row (a continuation is then let through with a warning and an incident in the log). While a live recycle waiter
/// is recorded for the meta lane, a blocked stop is let through too, so the waiter sees the turn end and types /clear (#4264); the open
/// obligations stay in the store and the next session's stop-check sees them again.
/// </summary>
/// <param name="session">Tells the meta session from the rest.</param>
/// <param name="sweep">Syncs, refreshes and evaluates.</param>
/// <param name="guard">The loop guard.</param>
/// <param name="clock">Dates the incident.</param>
/// <param name="beats">The lanes' stop beats; the meta's own stop is recorded there.</param>
internal sealed class StopCheckService(MetaSession session, MetaSweep sweep, StopGuard guard, TimeProvider clock, LaneBeats beats) : IService<StopCheckRequest, StopCheckResponse>
{
    // How many open obligations the carry-over warning names.
    private const int Shown = 3;

    public async ValueTask<Result<StopCheckResponse>> Handle(StopCheckRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var team = await session.Resolve(request.Session, cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return team.Error;
        }

        if (team.Value is not { } config)
        {
            return new StopCheckResponse(IsMeta: false, Blocked: false, [], Warning: null);
        }

        var now = clock.GetUtcNow();
        var open = await sweep.Run(request.Transcript, config, now, cancellationToken).ConfigureAwait(false);
        if (open.IsError)
        {
            return open.Error;
        }

        var outcome = await Decide(config, request, open.Value, now, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.Error;
        }

        // The idle signal a detached recycle waits for: a blocked stop is a beat too (the hook ran) but not a stopped one.
        var beat = await beats.Record(config.MetaLane, stopped: !outcome.Value.Block, now, cancellationToken).ConfigureAwait(false);
        if (beat.IsError)
        {
            return beat.Error;
        }

        return new StopCheckResponse(IsMeta: true, outcome.Value.Block, open.Value, outcome.Value.Warning);
    }

    // The meta nearly always owes something, so a waiter that waits for a turn end gave up every time (#4264). A dead waiter pid or no
    // waiter changes nothing.
    private async ValueTask<Result<StopOutcome>> Decide(TeamConfig config, StopCheckRequest request, IReadOnlyList<Obligation> open, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var outcome = await guard.Decide(open, request.HookActive, request.MaxBlocks, now, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError || !outcome.Value.Block || !await sweep.RecycleWaiterRunning(config.MetaLane, cancellationToken).ConfigureAwait(false))
        {
            return outcome;
        }

        return new StopOutcome(Block: false, CarryOver(open));
    }

    private static string CarryOver(IReadOnlyList<Obligation> open)
    {
        var count = open.Count == 1 ? "1 open obligation carries over" : string.Create(CultureInfo.InvariantCulture, $"{open.Count} open obligations carry over");
        var more = open.Count > Shown ? string.Create(CultureInfo.InvariantCulture, $"; and {open.Count - Shown} more") : string.Empty;
        return "aw-meta: letting the turn end for the pending recycle; " + count + ": " + string.Join("; ", open.Take(Shown).Select(static o => o.Summary)) + more;
    }
}
