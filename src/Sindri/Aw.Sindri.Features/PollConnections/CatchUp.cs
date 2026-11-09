using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What one read of a (connection, event kind) did, summed over its provider calls.</summary>
/// <param name="Events">Events the provider returned, each delivery id counted once.</param>
/// <param name="Started">Runs started or queued.</param>
/// <param name="Skipped">Triggers skipped.</param>
/// <param name="Capped">True when the read stopped at a cap and recorded catchup.capped.</param>
/// <param name="RateLimited">True when the provider asked to wait.</param>
internal sealed record ReadOutcome(int Events, int Started, int Skipped, bool Capped, bool RateLimited);

/// <summary>
/// Reads one (connection, event kind) forward: a regular poll is one provider call; a catch-up (on control-plane start and after a wake) repeats the call, saving the cursor after each, until a call brings
/// nothing new or <c>pages</c> calls are done. Each provider call itself reads up to its own page cap (<see cref="PollLimits.DefaultPageCap"/>). A read that is still getting new events at the cap, or whose
/// poller reported a gap, records ONE catchup.capped and leaves the cursor where it stopped; the next poll goes on from there. A repeat of an event (a poller may overlap) is told apart by its delivery id
/// and never counts as new, so an overlap cannot keep the loop going and starts no second run. Records 'connection.polled' once when the read saw events.
/// </summary>
/// <param name="round">One provider call and its fan-out.</param>
/// <param name="events">Records the capped read and the counts.</param>
internal sealed class CatchUp(PollRound round, ConnectionEvents events)
{
    internal async ValueTask<Result<ReadOutcome>> Read(PollGroup group, Connection connection, IEventPoller poller, PollCursor? cursor, int pages, bool forward, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var started = 0;
        var skipped = 0;
        var capped = false;
        string? reason = null;
        var rateLimited = false;
        var settled = false;
        var calls = forward ? Math.Max(pages, 1) : 1;
        for (var call = 0; call < calls && !settled; call++)
        {
            var outcome = await round.Run(group, connection, poller, cursor, grant, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;
            }

            var step = outcome.Value;
            cursor = step.Cursor;
            started += step.Started;
            skipped += step.Skipped;
            rateLimited = step.RateLimited;
            var fresh = step.Keys.Count(seen.Add);
            if (step.Gap)
            {
                capped = true;
                reason ??= step.Reason ?? "the provider's page cap";
            }

            // why: a call that brought nothing new, did not move the cursor or was rate limited has nothing left to read; the loop ends there, capped or not
            settled = step.RateLimited || fresh == 0 || !step.CursorMoved;
            if (!settled && forward && call == calls - 1)
            {
                capped = true;
                reason ??= string.Create(CultureInfo.InvariantCulture, $"more than {calls} pages");
            }
        }

        if (seen.Count > 0)
        {
            await events.Polled(new PollCounts(group.Connection, group.Kind, seen.Count, started, skipped), grant, cancellationToken).ConfigureAwait(false);
        }

        if (capped)
        {
            await events.CatchUpStopped(group.Connection, group.Kind, reason ?? "the page cap", grant, cancellationToken).ConfigureAwait(false);
        }

        return new ReadOutcome(seen.Count, started, skipped, capped, rateLimited);
    }
}
