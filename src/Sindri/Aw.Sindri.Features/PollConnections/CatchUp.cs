using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// Reads one (connection, event kind) forward: a regular poll is one provider call; a catch-up (on control-plane start and after a wake) repeats the call, saving the cursor after each, until a call brings
/// nothing new or <c language="text">pages</c> calls are done. Each provider call itself reads up to its own page cap (<see cref="PollLimits.DefaultPageCap"/>). A read that is still getting new events at the cap, or whose
/// poller reported a gap, records ONE catchup.capped and leaves the cursor where it stopped; the next poll goes on from there. A repeat of an event (a poller may overlap) is told apart by its delivery id
/// and never counts as new, so an overlap cannot keep the loop going and starts no second run. Records 'connection.polled' once when the read saw events.
/// </summary>
/// <param name="round">One provider call and its fan-out.</param>
/// <param name="events">Records the capped read and the counts.</param>
internal sealed class CatchUp(PollRound round, ConnectionEvents events)
{
    internal async ValueTask<Result<ReadOutcome>> Read(ReadRequest request, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tally = new Tally();
        var cursor = request.Cursor;
        var calls = request.Forward ? Math.Max(request.Pages, 1) : 1;
        for (var call = 0; call < calls && !tally.Settled; call++)
        {
            var outcome = await round.Run(new RoundRequest(request.Group, request.Connection, request.Poller, cursor), grant, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;
            }

            cursor = outcome.Value.Cursor;
            tally.Add(outcome.Value, request.Forward && call == calls - 1, calls);
        }

        await Record(request.Group, tally, grant, cancellationToken).ConfigureAwait(false);
        return tally.ToOutcome();
    }

    private async ValueTask Record(PollGroup group, Tally tally, LeaseGrant grant, CancellationToken cancellationToken)
    {
        if (tally.Seen.Count > 0)
        {
            await events.Polled(new PollCounts(group.Connection, group.Kind.Key, tally.Seen.Count, tally.Started, tally.Skipped), grant, cancellationToken).ConfigureAwait(false);
        }

        if (tally.Capped)
        {
            await events.CatchUpStopped(group.Connection, group.Kind.Key, tally.Reason ?? "the page cap", grant, cancellationToken).ConfigureAwait(false);
        }
    }

    // The running sums of one read, one provider call at a time.
    private sealed class Tally
    {
        internal HashSet<string> Seen { get; } = new(StringComparer.Ordinal);

        internal int Started { get; private set; }

        internal int Skipped { get; private set; }

        internal bool Capped { get; private set; }

        internal string? Reason { get; private set; }

        internal bool RateLimited { get; private set; }

        internal bool Settled { get; private set; }

        internal void Add(RoundOutcome step, bool lastForwardCall, int calls)
        {
            Started += step.Started;
            Skipped += step.Skipped;
            RateLimited = step.RateLimited;
            var fresh = step.Keys.Count(Seen.Add);
            if (step.Gap)
            {
                Cap(step.Reason ?? "the provider's page cap");
            }

            // why: a call that brought nothing new, did not move the cursor or was rate limited has nothing left to read; the loop ends there, capped or not
            Settled = step.RateLimited || fresh == 0 || !step.CursorMoved;
            if (!Settled && lastForwardCall)
            {
                Cap(string.Create(CultureInfo.InvariantCulture, $"more than {calls} pages"));
            }
        }

        internal ReadOutcome ToOutcome() => new(Seen.Count, Started, Skipped, Capped, RateLimited);

        private void Cap(string reason)
        {
            Capped = true;
            Reason ??= reason;
        }
    }
}
