using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.HomeCollect;

/// <summary>
/// Appends one collector pass's plan as 'home.row.raised', '.changed' and '.cleared' events (home spec §3.1), fenced by the pass's lease grant.
/// Every key carries the pass's slot, so a replayed slot writes nothing twice while the next episode of the same row (a lane at its mark again) is a new event.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="events">Builds the entries.</param>
internal sealed class HomeRowWriter(ILedgerStore ledger, EventFactory events)
{
    /// <summary>Writes <paramref name="plan"/>: raises first, then changes, then clears. It stops at the first append that fails.</summary>
    /// <param name="plan">The pass's plan.</param>
    /// <param name="run">The pass: its grant and its slot.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>How many events were new, or the failed append's error.</returns>
    public async ValueTask<Result<int>> Write(HomePlan plan, ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(run);
        var slot = run.Slot.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var entries = new List<LedgerEntry>();
        entries.AddRange(plan.Raised.Select(r => events.Entry(EventTypes.HomeRowRaised, new HomeRowRaised(r.Row, r.Edges), $"{EventTypes.HomeRowRaised}:{r.Row.Id.Value}:{slot}")));
        entries.AddRange(plan.Changed.Select(c => events.Entry(EventTypes.HomeRowChanged, Changed(c), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.HomeRowChanged}:{c.Row.Id.Value}:v{c.Row.Version}:{slot}"))));
        entries.AddRange(plan.Cleared.Select(id => events.Entry(EventTypes.HomeRowCleared, new HomeRowCleared(id), $"{EventTypes.HomeRowCleared}:{id.Value}:{slot}")));
        var written = 0;
        foreach (var entry in entries)
        {
            var appended = await ledger.Append(entry, run.Grant, cancellationToken).ConfigureAwait(false);
            if (appended.IsError)
            {
                return appended.Error;
            }

            written += appended.Value.AlreadyRecorded ? 0 : 1;
        }

        return written;
    }

    // why: a change carries the source's fields only; the rank and blocks list are the ranker's (A1), so they stay null and the view keeps what it has
    private static HomeRowChanged Changed(HomeRaise change) =>
        new(change.Row.Id, change.Row.Version, change.Row.Title, change.Row.Facts, change.Row.WaitsOn, Rank: null, BlocksList: null, change.Row.Links, change.Row.Actions, change.Row.Source, change.Edges);
}
