using Aw.Redaction;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Appends a graph run's events to the ledger (issue 2258, PR 6), fenced by the held lease. The key names the fact ('workflow.node.finished:&lt;run&gt;:&lt;node&gt;:&lt;item&gt;'),
/// so an activity that runs twice records it once. A failure throws and Temporal retries the activity.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="lease">The lease this process holds.</param>
/// <param name="events">The control plane's event factory.</param>
/// <param name="redactor">Scrubs free text (an error, a trigger) before it is stored; the built-in rules when none is given.</param>
internal sealed class GraphMirror(ILedgerStore ledger, HeldLease lease, EventFactory events, IRedactor? redactor = null)
{
    /// <summary>The 'By' every graph event carries.</summary>
    public const string By = "service:workflow";

    /// <summary>The word a status is stored under.</summary>
    /// <param name="status">The run status.</param>
    /// <returns>'queued', 'running', 'waiting', 'succeeded', 'failed' or 'cancelled'.</returns>
    public static string Name(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Queued => "queued",
        WorkflowRunStatus.Running => "running",
        WorkflowRunStatus.Waiting => "waiting",
        WorkflowRunStatus.Succeeded => "succeeded",
        WorkflowRunStatus.Failed => "failed",
        WorkflowRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>The word a node status is stored under.</summary>
    /// <param name="status">The node status.</param>
    /// <returns>'running', 'waiting', 'succeeded', 'failed', 'skipped' or 'cancelled'.</returns>
    public static string Name(NodeRunStatus status) => status switch
    {
        NodeRunStatus.Running => "running",
        NodeRunStatus.Waiting => "waiting",
        NodeRunStatus.Succeeded => "succeeded",
        NodeRunStatus.Failed => "failed",
        NodeRunStatus.Skipped => "skipped",
        NodeRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>Scrubs free text.</summary>
    /// <param name="text">The text, or null.</param>
    /// <returns>The scrubbed text; a text the scrub blocked is replaced by a marker, never passed on.</returns>
    public string? Clean(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var result = (redactor ?? Redactor.Default).Scrub(text);
        return result.IsBlocked ? "[blocked by the redactor]" : result.Text;
    }

    /// <summary>Appends 'workflow.run.started' for a run. The key is the run's, so it is recorded once however many times it is asked (the begin step, and an end that finds no start).</summary>
    /// <param name="run">The run row.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Completes when the event is in the ledger.</returns>
    public async Task AppendStarted(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var payload = new WorkflowRunStartedPayload(run.Id, run.WorkflowId, run.Revision, Clean(run.Trigger) ?? string.Empty, By, WorkflowRunMode.Live);
        await Append(EventTypes.WorkflowRunStarted, payload, $"{EventTypes.WorkflowRunStarted}:{run.Id.Value}", run.ItemId?.Value, cancellationToken).ConfigureAwait(false);
        if (run.ItemId is { } item)
        {
            // why: every valid run id fits a record key, so a mismatch throws instead of dropping the link (issue 2433)
            var record = RecordKey.From("sindri:run/" + run.Id.Value);
            // the item shows the run it works for (issue 2433); keyed by item and record, so a repeat is recorded once
            var linked = new ItemLinked(CommandFor(record), item, record, Alias: null);
            await Append(EventTypes.ItemLinked, linked, $"{EventTypes.ItemLinked}:{item.Value}:{record.Value}", item.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    // no command made this link: a stable id taken from the run's record, so a repeat carries the same one
    private static CommandId CommandFor(RecordKey record) =>
        CommandId.From(new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(record.Value)).AsSpan(0, 16)));

    /// <summary>Appends one event.</summary>
    /// <param name="type">The event type.</param>
    /// <param name="payload">The payload record.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="item">The item the run works for, or null.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <typeparam name="TPayload">The payload type.</typeparam>
    /// <returns>Completes when the event is in the ledger.</returns>
    public async Task Append<TPayload>(string type, TPayload payload, string key, string? item, CancellationToken cancellationToken)
    {
        var grant = lease.Current ?? throw new InvalidOperationException("SND-LEASE-002: this process holds no lease; the event waits for one");
        var scope = new EventScope(By, events.Scope.Tenant, events.Scope.Ring);
        var appended = await ledger.Append(events.Entry(type, payload, key, item, scope), grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            throw new InvalidOperationException($"{appended.Error.Code}: {appended.Error.Message}");
        }
    }
}
