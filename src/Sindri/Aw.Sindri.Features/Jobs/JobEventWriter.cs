using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// Appends a job's own events ('job.progress', 'job.finished') as 'service:jobs' (API spec §2.1), under the start's correlation, with the job's start
/// event as their causation, fenced by the held lease (API P6 unit C). Lines, summaries and results are cleaned here, once, at write time (D11).
/// </summary>
/// <param name="ledger">Appends the events.</param>
/// <param name="held">The lease every write is fenced by.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="scrubber">Cleans lines, summaries and results.</param>
/// <param name="callers">Where 'service:jobs' is entered.</param>
internal sealed class JobEventWriter(ILedgerStore ledger, HeldLease held, EventFactory events, ITextScrubber scrubber, ICallerContextAccessor callers)
{
    private const string ResultTooLarge = " (result too large)";
    private const string InterruptedSummary = "interrupted: the process stopped while it ran";

    /// <summary>Whether the control plane still holds the lease: a failed write with the lease held is the runner's to retry and then end the job over.</summary>
    public bool HoldsLease() => held.Current is not null;

    /// <summary>Records one progress line (cleaned here) as round <paramref name="round"/>.</summary>
    /// <param name="start">The job.</param>
    /// <param name="round">Its number.</param>
    /// <param name="line">The raw line.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the line was not recorded (no lease, or a lost one).</returns>
    public ValueTask<Result<Unit>> Progress(JobStart start, int round, string line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(line);
        var payload = new JobProgress(start.Caller.CommandId, start.Job, round, JobLines.Clean(scrubber, line), Item: null, start.Repo);
        return Append(start, EventTypes.JobProgress, payload, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{EventTypes.JobProgress}:{start.Job.Value}:{round}"), cancellationToken);
    }

    /// <summary>Records that the job parked on <paramref name="signal"/> (its <paramref name="ordinal"/>th wait).</summary>
    /// <param name="start">The job.</param>
    /// <param name="signal">The signal's name.</param>
    /// <param name="ordinal">Which wait of the job, from 1.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the wait was not recorded.</returns>
    public ValueTask<Result<Unit>> Waiting(JobStart start, string signal, int ordinal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(signal);
        var payload = new JobWaiting(start.Caller.CommandId, start.Job, signal, Item: null, start.Repo);
        return Append(start, EventTypes.JobWaiting, payload, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{EventTypes.JobWaiting}:{start.Job.Value}:{ordinal}"), cancellationToken);
    }

    /// <summary>Records that the signal came and the job runs again.</summary>
    /// <param name="start">The job.</param>
    /// <param name="signal">The signal's name.</param>
    /// <param name="ordinal">Which wait of the job, from 1.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why it was not recorded.</returns>
    public ValueTask<Result<Unit>> Resumed(JobStart start, string signal, int ordinal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(signal);
        var payload = new JobResumed(start.Caller.CommandId, start.Job, signal, Item: null, start.Repo);
        return Append(start, EventTypes.JobResumed, payload, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{EventTypes.JobResumed}:{start.Job.Value}:{ordinal}"), cancellationToken);
    }

    /// <summary>Records how the job ended.</summary>
    /// <param name="start">The job.</param>
    /// <param name="work">How its work ended.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the end was not recorded.</returns>
    public ValueTask<Result<Unit>> Finish(JobStart start, JobWorkResult work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(work);
        var payload = work.Error is { } error
            ? new JobFinished(start.Caller.CommandId, start.Job, JobOutcome.Failed, JobLines.Clean(scrubber, error.Message), error.Code, Result: null, Item: null, start.Repo)
            : Succeeded(start, work);
        return Append(start, EventTypes.JobFinished, payload, $"{EventTypes.JobFinished}:{start.Job.Value}", cancellationToken);
    }

    /// <summary>Records that a job the previous process left running was interrupted by that process's end (recovery, API P6 unit F). The key is the job's end key, so a replay writes nothing.</summary>
    /// <param name="job">The row of the job that was running.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when the end was written now, false when it was there already, or why it was not recorded (no lease, or a lost one).</returns>
    public async ValueTask<Result<bool>> Interrupted(JobRow job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease");
        }

        using var entered = callers.EnterService("jobs");
        if (callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("no caller to record the end under");
        }

        var payload = new JobFinished(caller.CommandId, job.Id, JobOutcome.Failed, InterruptedSummary, SindriErrors.JobInterrupted.Code, Result: null, job.Item, job.Repo);
        var appended = await ledger.Append(events.Entry(EventTypes.JobFinished, payload, $"{EventTypes.JobFinished}:{job.Id.Value}"), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : !appended.Value.AlreadyRecorded;
    }

    private JobFinished Succeeded(JobStart start, JobWorkResult work)
    {
        var summary = JobLines.Clean(scrubber, work.Summary);
        var result = Kept(work.Result, out var note);
        return new JobFinished(start.Caller.CommandId, start.Job, JobOutcome.Succeeded, summary + note, Code: null, result, Item: null, start.Repo);
    }

    /// <summary>The result with every string (and property name) scrubbed then escaped, bounded to <see cref="JobLimits.ResultBytes"/>; null when there is none, it is over the bound, or it can't be rewritten.</summary>
    private JsonElement? Kept(JsonElement? result, out string? note)
    {
        note = null;
        if (result is not { } element)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(element, writer);
        }

        if (buffer.Length > JobLimits.ResultBytes)
        {
            note = ResultTooLarge;
            return null;
        }

        using var document = JsonDocument.Parse(buffer.ToArray());   // what the writer wrote is JSON: AddJob already turned the result into an element
        return document.RootElement.Clone();
    }

    private string Safe(string text) => SafeText.Escape(scrubber.Scrub(text));

    private void Write(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            writer.WriteStringValue(Safe(element.GetString() ?? string.Empty));
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(Safe(property.Name));
                Write(property.Value, writer);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                Write(item, writer);
            }

            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
    }

    private async ValueTask<Result<Unit>> Append<TPayload>(JobStart start, string type, TPayload payload, string key, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease");
        }

        var jobs = new CallerContext(CallerClass.Service, Terminal: false, "service:jobs", start.Caller.CommandId, start.Caller.CorrelationId, Repo: null);
        using var entered = callers.Enter(jobs);
        var entry = events.Entry(type, payload, key);
        entry = entry with { Envelope = entry.Envelope with { CausationId = start.Cause?.Value } };
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
