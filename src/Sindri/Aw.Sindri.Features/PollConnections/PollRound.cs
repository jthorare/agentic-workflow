using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>What one provider call did.</summary>
/// <param name="Cursor">The cursor as it stands after the call (saved when the call succeeded).</param>
/// <param name="Keys">The delivery ids the provider returned.</param>
/// <param name="Started">Runs started or queued.</param>
/// <param name="Skipped">Triggers skipped (a run in flight, or the delivery already had its run).</param>
/// <param name="CursorMoved">True when the saved cursor value changed.</param>
/// <param name="RateLimited">True when the provider asked to wait; the backoff is saved and nothing else changed.</param>
/// <param name="Gap">True when the poller hit its own page cap and skipped older events.</param>
/// <param name="Reason">The poller's reason for the gap, or null.</param>
internal sealed record RoundOutcome(PollCursor Cursor, IReadOnlyList<string> Keys, int Started, int Skipped, bool CursorMoved, bool RateLimited, bool Gap, string? Reason);

/// <summary>
/// One provider call for one (connection, event kind) and what follows it: the events fan out to the triggers that watch the group, each trigger starts a run whose occurrence is the delivery id,
/// and only then is the cursor saved. A start that fails for a reason other than "this delivery already has its run" (a Temporal outage, a store error) stops the round with the cursor where it was,
/// so the next poll reads the same events again; the runs that did start are refused there as duplicates. A rate-limited answer or an error saves the backoff and keeps the cursor value.
/// </summary>
/// <param name="starter">Starts, queues or skips a run.</param>
/// <param name="cursors">Where cursors are saved.</param>
/// <param name="events">Records the skipped triggers.</param>
/// <param name="clock">The time.</param>
/// <param name="runs">Reads an existing run, to tell an exact repeat of a delivery from another delivery that hashed to the same occurrence.</param>
/// <param name="occurrenceOf">The delivery id to occurrence function; <see cref="DeliveryOccurrence.Of"/> when null (a test forces a collision).</param>
internal sealed class PollRound(IGraphRunStarter starter, IPollCursors cursors, ConnectionEvents events, TimeProvider clock, IWorkflowRunStore runs, Func<string, int>? occurrenceOf = null)
{
    private const string Concurrency = "concurrency";
    private const string Duplicate = "duplicate";
    private const int Probes = 5;
    private const int Last = 999_999_999;
    private const int First = 100_000_000;

    internal async ValueTask<Result<RoundOutcome>> Run(PollGroup group, Connection connection, IEventPoller poller, PollCursor? saved, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(poller);
        var cursor = saved ?? new PollCursor(group.Connection, group.Kind, Value: null, UpdatedAt: null, LastPollAt: null, BackoffUntil: null, Failures: 0);
        var answered = await poller.Poll(connection, group.Kind, cursor.Value, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (answered.IsError)
        {
            var failed = PollBackoff.Next(cursor.Failures, retryAfter: null, now);
            _ = await cursors.Save(cursor with { LastPollAt = now, BackoffUntil = failed.Until, Failures = failed.Failures }, cancellationToken).ConfigureAwait(false);
            return answered.Error;
        }

        var poll = answered.Value;
        if (poll.RateLimited)
        {
            var wait = PollBackoff.Next(cursor.Failures, poll.RetryAfter, now);
            var waiting = cursor with { LastPollAt = now, BackoffUntil = wait.Until, Failures = wait.Failures };
            var kept = await cursors.Save(waiting, cancellationToken).ConfigureAwait(false);
            return kept.IsError ? kept.Error : new RoundOutcome(waiting, [], Started: 0, Skipped: 0, CursorMoved: false, RateLimited: true, Gap: false, poll.Reason);
        }

        var started = 0;
        var skipped = 0;
        foreach (var polled in poll.Events)
        {
            foreach (var trigger in group.Triggers.Where(t => TriggerMatch.Wants(t.Filter, group.Connection, polled)))
            {
                var outcome = await Start(group, trigger, polled, grant, cancellationToken).ConfigureAwait(false);
                if (outcome.IsError)
                {
                    return outcome.Error;   // why: the cursor stays where it was, so the next poll reads these events again
                }

                started += outcome.Value ? 1 : 0;
                skipped += outcome.Value ? 0 : 1;
            }
        }

        var value = poll.NextCursor ?? cursor.Value;
        var moved = !string.Equals(value, cursor.Value, StringComparison.Ordinal);
        var after = cursor with { Value = value, UpdatedAt = moved ? now : cursor.UpdatedAt, LastPollAt = now, BackoffUntil = null, Failures = 0 };
        var written = await cursors.Save(after, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new RoundOutcome(after, [.. poll.Events.Select(static e => e.DeliveryKey)], started, skipped, moved, RateLimited: false, poll.Gap, poll.Reason);
    }

    // True when a run started or queued; false when the trigger was skipped (and recorded). Any other failure is the round's error.
    // why: the occurrence is a hash, so two deliveries can share one. SND-GRAPH-100 is a duplicate only when the run that holds the id carries THIS delivery id; otherwise the next occurrence number is tried.
    private async ValueTask<Result<bool>> Start(PollGroup group, PollTrigger trigger, PolledEvent polled, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var json = TriggerMatch.Trigger(group.Connection, polled);
        var occurrence = (occurrenceOf ?? DeliveryOccurrence.Of)(polled.DeliveryKey);
        for (var probe = 0; probe < Probes; probe++)
        {
            var request = new GraphStartRequest(trigger.Workflow, trigger.Slug, trigger.Revision, occurrence, json, trigger.Concurrency);
            var begun = await starter.Start(request, cancellationToken).ConfigureAwait(false);
            if (begun.IsError && string.Equals(begun.Error.Code, SindriErrors.GraphRunIdTaken.Code, StringComparison.Ordinal))
            {
                var holder = await runs.Get(WorkflowRunId.Of(trigger.Slug, trigger.Revision, occurrence), cancellationToken).ConfigureAwait(false);
                if (holder.IsError)
                {
                    return holder.Error;
                }

                if (string.Equals(DeliveryOf(holder.Value.Trigger), polled.DeliveryKey, StringComparison.Ordinal))
                {
                    await events.TriggerSkipped(trigger.Slug.Value, group.Connection, group.Kind, polled.DeliveryKey, Duplicate, grant, cancellationToken).ConfigureAwait(false);
                    return false;
                }

                occurrence = occurrence >= Last ? First : occurrence + 1;
                continue;
            }

            if (begun.IsError)
            {
                return begun.Error;
            }

            if (begun.Value.Kind == GraphStartKind.Skipped)
            {
                await events.TriggerSkipped(trigger.Slug.Value, group.Connection, group.Kind, polled.DeliveryKey, Concurrency, grant, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        return SindriErrors.GraphRunIdTaken.With($"{Probes} run ids in a row are held by other deliveries; the cursor stays and the next poll tries again");   // why: never drop a real event silently
    }

    private static string? DeliveryOf(string trigger)
    {
        try
        {
            return JsonNode.Parse(trigger) is JsonObject json && json["deliveryId"] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;
        }
        catch (System.Text.Json.JsonException)   // boundary: a trigger that is not JSON is not this delivery's
        {
            return null;
        }
    }
}
