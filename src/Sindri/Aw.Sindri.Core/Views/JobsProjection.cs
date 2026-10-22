using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// Folds the four job events (lines and summaries are escaped again on read, so a raw control byte that reached the ledger never reaches a client; escaping is idempotent) into the 'jobs' view (API P6): one row per job, keyed by its id, each event through the pure Fold methods below.
/// A progress, finish or cancel for a job with no row is a fold that can't continue (SND-VIEW-001): the ledger holds events the view can't explain.
/// </summary>
/// <param name="events">Reads the payloads through the package's AwJson options.</param>
internal sealed class JobsProjection(EventFactory events) : IProjection
{
    public IReadOnlyList<string> Views => [ViewNames.Jobs];

    public bool Handles(string eventType) =>
        string.Equals(eventType, EventTypes.JobStarted, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobProgress, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobFinished, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobCancelled, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobWaiting, StringComparison.Ordinal)
        || string.Equals(eventType, EventTypes.JobResumed, StringComparison.Ordinal);

    public async ValueTask<Result<Unit>> Apply(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        ArgumentNullException.ThrowIfNull(rows);
        return recorded.Envelope.Type switch
        {
            EventTypes.JobStarted => await Started(recorded, rows, cancellationToken).ConfigureAwait(false),
            EventTypes.JobProgress => await Change<JobProgress>(recorded, rows, static p => p.Job, Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.JobFinished => await Change<JobFinished>(recorded, rows, static f => f.Job, Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.JobWaiting => await Change<JobWaiting>(recorded, rows, static w => w.Job, Fold, cancellationToken).ConfigureAwait(false),
            EventTypes.JobResumed => await Change<JobResumed>(recorded, rows, static r => r.Job, Fold, cancellationToken).ConfigureAwait(false),
            _ => await Change<JobCancelled>(recorded, rows, static c => c.Job, Fold, cancellationToken).ConfigureAwait(false),
        };
    }

    private async ValueTask<Result<Unit>> Started(LedgerEvent recorded, IViewRows rows, CancellationToken cancellationToken)
    {
        var started = events.Payload<JobStarted>(recorded);
        if (started.IsError)
        {
            return started.Error;
        }

        var key = started.Value.Job.Value;
        var row = await rows.Get<JobRow>(ViewNames.Jobs, key, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Jobs, key, Fold(row.Value, started.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<Unit>> Change<TPayload>(LedgerEvent recorded, IViewRows rows, Func<TPayload, JobId> job,
        Func<JobRow, TPayload, LedgerEvent, JobRow> fold, CancellationToken cancellationToken)
    {
        var payload = events.Payload<TPayload>(recorded);
        if (payload.IsError)
        {
            return payload.Error;
        }

        var key = job(payload.Value).Value;
        var row = await rows.Require<JobRow>(ViewNames.Jobs, key, recorded.Envelope.Type, cancellationToken).ConfigureAwait(false);
        return row.IsError ? row.Error : await rows.Put(ViewNames.Jobs, key, fold(row.Value, payload.Value, recorded), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The row a 'job.started' makes: running, no lines yet. A replay (the row exists) changes nothing.</summary>
    /// <param name="row">The row so far, or null before the job's first event.</param>
    /// <param name="started">The payload.</param>
    /// <param name="recorded">The recorded event (its time, actor and sequence).</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow? row, JobStarted started, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(started);
        ArgumentNullException.ThrowIfNull(recorded);
        return row ?? new JobRow(started.Job, started.Kind, started.Key, started.Item, started.Repo, started.Service, JobState.Running,
            recorded.Envelope.Actor, recorded.Envelope.OccurredAt, EndedAt: null, Progress: [], Rounds: 0, Summary: null, Code: null, Result: null,
            CancelledBy: null, recorded.Sequence);
    }

    /// <summary>The row after a 'job.progress': the line is appended, the last 200 are kept and the rounds are the highest seen. A job that ended ignores it (D5).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="progress">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobProgress progress, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(recorded);
        return !JobStates.IsActive(row.State)
            ? row
            : row with
            {
                Progress = JobLines.Keep(row.Progress, new JobLine(progress.Round, SafeText.Escape(progress.Line), recorded.Envelope.OccurredAt)),
                Rounds = Math.Max(row.Rounds, progress.Round),
                Sequence = recorded.Sequence,
            };
    }

    /// <summary>The row after a 'job.finished': succeeded or failed, with its summary, code and result. A job that ended ignores it (D5: the first terminal event wins).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="finished">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobFinished finished, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(finished);
        ArgumentNullException.ThrowIfNull(recorded);
        return !JobStates.IsActive(row.State)
            ? row
            : row with
            {
                State = finished.Outcome == JobOutcome.Succeeded ? JobState.Succeeded : JobState.Failed,
                EndedAt = recorded.Envelope.OccurredAt,
                Summary = SafeText.Escape(finished.Summary),
                Code = finished.Code,
                Result = finished.Result,
                Sequence = recorded.Sequence,
            };
    }

    /// <summary>The row after a 'job.waiting': waiting, when it was running. Anything else (it already ended, or waits) is as it was.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="waiting">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobWaiting waiting, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(waiting);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.State != JobState.Running ? row : row with { State = JobState.Waiting, Sequence = recorded.Sequence };
    }

    /// <summary>The row after a 'job.resumed': running again, when it was waiting.</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="resumed">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobResumed resumed, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(resumed);
        ArgumentNullException.ThrowIfNull(recorded);
        return row.State != JobState.Waiting ? row : row with { State = JobState.Running, Sequence = recorded.Sequence };
    }

    /// <summary>The row after a 'job.cancelled': cancelled, by whom. A job that ended ignores it (D5).</summary>
    /// <param name="row">The row so far.</param>
    /// <param name="cancelled">The payload.</param>
    /// <param name="recorded">The recorded event.</param>
    /// <returns>The row.</returns>
    public static JobRow Fold(JobRow row, JobCancelled cancelled, LedgerEvent recorded)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(cancelled);
        ArgumentNullException.ThrowIfNull(recorded);
        return !JobStates.IsActive(row.State)
            ? row
            : row with { State = JobState.Cancelled, EndedAt = recorded.Envelope.OccurredAt, Summary = "cancelled", CancelledBy = cancelled.By, Sequence = recorded.Sequence };
    }
}
