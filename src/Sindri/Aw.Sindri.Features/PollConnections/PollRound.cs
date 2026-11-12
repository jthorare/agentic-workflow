using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>
/// One provider call for one (connection, event kind) and what follows it: the events fan out to the triggers that watch the group, each trigger starts a run whose occurrence is the delivery id,
/// and only then is the cursor saved. A start that fails for a reason other than "this delivery already has its run" (a Temporal outage, a store error) stops the round with the cursor where it was,
/// so the next poll reads the same events again after the backoff; the runs that did start are refused there as duplicates. A launch that fails for any other reason keeps its failed run under the delivery's id
/// (<c language="text">GraphRunStarter</c>), so that event is a duplicate on the next poll and cannot hold the group back. A rate-limited answer, a poller error or a failed start saves the backoff and keeps the cursor value.
/// </summary>
/// <param name="starter">Starts, queues or skips a run.</param>
/// <param name="cursors">Where cursors are saved.</param>
/// <param name="events">Records the skipped triggers.</param>
/// <param name="clock">The time.</param>
/// <param name="runs">Reads an existing run, to tell an exact repeat of a delivery from another delivery that hashed to the same occurrence.</param>
/// <param name="recent">Keeps every event a poll returned, for the trigger step's recent events (#3201).</param>
/// <param name="occurrenceOf">The delivery id to occurrence function; <see cref="DeliveryOccurrence.Of"/> when null (a test forces a collision).</param>
internal sealed class PollRound(IGraphRunStarter starter, IPollCursors cursors, ConnectionEvents events, TimeProvider clock, IWorkflowRunStore runs, RecentEvents recent, Func<string, int>? occurrenceOf = null)
{
    private const string Concurrency = "concurrency";
    private const string Duplicate = "duplicate";
    private const int Probes = 5;
    private const int Last = 999_999_999;
    private const int First = 100_000_000;

    internal async ValueTask<Result<RoundOutcome>> Run(RoundRequest request, LeaseGrant grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var group = request.Group;
        var cursor = request.Cursor ?? new PollCursor(group.Connection, group.Kind, Value: null, UpdatedAt: null, LastPollAt: null, BackoffUntil: null, Failures: 0);
        var answered = await request.Poller.Poll(request.Connection, group.Kind, cursor.Value, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (answered.IsError)
        {
            return await Failed(cursor, answered.Error, now, cancellationToken).ConfigureAwait(false);
        }

        var poll = answered.Value;
        if (poll.RateLimited)
        {
            return await SaveRateLimit(cursor, poll, now, cancellationToken).ConfigureAwait(false);
        }

        foreach (var polled in poll.Events)
        {
            recent.Record(group.Connection, polled);   // why: seen is seen, whether or not a trigger's filter takes it or its start fails (the trigger step lists it)
        }

        var fanned = await FanOut(group, poll, grant, cancellationToken).ConfigureAwait(false);
        return fanned.IsError
            ? await Failed(cursor, fanned.Error, now, cancellationToken).ConfigureAwait(false)
            : await Advance(cursor, poll, fanned.Value, now, cancellationToken).ConfigureAwait(false);
    }

    // A poller error or a failed start: the cursor value stays and the backoff is saved, so a Temporal outage or a store error is not retried against the provider every 30 s tick.
    private async ValueTask<Result<RoundOutcome>> Failed(PollCursor cursor, Error error, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var failed = PollBackoff.Next(cursor.Failures, retryAfter: null, now);
        _ = await cursors.Save(cursor with { LastPollAt = now, BackoffUntil = failed.Until, Failures = failed.Failures }, cancellationToken).ConfigureAwait(false);
        return error;
    }

    // A rate-limited answer saves the backoff and keeps the cursor value.
    private async ValueTask<Result<RoundOutcome>> SaveRateLimit(PollCursor cursor, PollResult poll, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var wait = PollBackoff.Next(cursor.Failures, poll.RetryAfter, now);
        var waiting = cursor with { LastPollAt = now, BackoffUntil = wait.Until, Failures = wait.Failures };
        var kept = await cursors.Save(waiting, cancellationToken).ConfigureAwait(false);
        return kept.IsError ? kept.Error : new RoundOutcome(waiting, [], Started: 0, Skipped: 0, CursorMoved: false, RateLimited: true, Gap: false, poll.Reason);
    }

    // Each event goes to every trigger whose filter takes it; the first failed start stops the round.
    private async ValueTask<Result<Fan>> FanOut(PollGroup group, PollResult poll, LeaseGrant grant, CancellationToken cancellationToken)
    {
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

        return new Fan(started, skipped);
    }

    // Saved only after every run started.
    private async ValueTask<Result<RoundOutcome>> Advance(PollCursor cursor, PollResult poll, Fan fan, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var value = poll.NextCursor ?? cursor.Value;
        var moved = !string.Equals(value, cursor.Value, StringComparison.Ordinal);
        var after = cursor with { Value = value, UpdatedAt = moved ? now : cursor.UpdatedAt, LastPollAt = now, BackoffUntil = null, Failures = 0 };
        var written = await cursors.Save(after, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new RoundOutcome(after, [.. poll.Events.Select(static e => e.DeliveryKey)], fan.Started, fan.Skipped, moved, RateLimited: false, poll.Gap, poll.Reason);
    }

    private sealed record Fan(int Started, int Skipped);

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
                    await events.TriggerSkipped(new SkippedTrigger(trigger.Slug.Value, group.Connection, group.Kind.Key, polled.DeliveryKey, Duplicate), grant, cancellationToken).ConfigureAwait(false);
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
                await events.TriggerSkipped(new SkippedTrigger(trigger.Slug.Value, group.Connection, group.Kind.Key, polled.DeliveryKey, Concurrency), grant, cancellationToken).ConfigureAwait(false);
                return false;
            }

            return true;
        }

        return SindriErrors.ConnectionRunIdsHeld.With($"{Probes} run ids in a row are held by other deliveries; the cursor stays and the next poll tries again");   // why: never drop a real event silently
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
