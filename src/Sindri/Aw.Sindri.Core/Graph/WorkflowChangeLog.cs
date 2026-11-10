using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Graph;

/// <summary>
/// Writes the workflow lifecycle events ('workflow.created', 'workflow.revised', 'workflow.activated', 'workflow.paused', 'workflow.archived'). A write slice checks
/// <see cref="LeaseProblem"/> before it touches the store, so a refusal leaves the workflow as it was, then calls <see cref="Record"/> once the store accepted the change.
/// The store save and the event append are two steps, not one transaction: when the append fails the change stays saved, so <see cref="Record"/>'s error names the saved
/// revision and the caller does not retry blind. (Both live in ledger.db; one transaction across the store and the ledger is the real fix and is not cheap.)
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller; its command id keys the events that can repeat.</param>
public sealed class WorkflowChangeLog(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers)
{
    /// <summary>Why no event can be written now.</summary>
    /// <returns>SND-API-001 when the control plane holds no lease, else null.</returns>
    public Error? LeaseProblem() => held.Current is null ? SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)") : null;

    /// <summary>Appends one lifecycle event.</summary>
    /// <param name="type">One of the five <see cref="EventTypes"/> workflow types.</param>
    /// <param name="payload">What changed.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the append failed (the message says the change itself is saved, and at which revision).</returns>
    public async ValueTask<Result<Unit>> Record(string type, WorkflowChangedPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);
        if (callers.Current is not { } caller)
        {
            return Saved(SindriErrors.AuthRefused.With("a workflow change needs a caller"), payload);
        }

        if (held.Current is not { } grant)
        {
            return Saved(SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)"), payload);
        }

        // why: the store already committed this change in its own transaction; a failed append leaves it in, so the error must say so
        var appended = await ledger.Append(events.Entry(type, payload, Key(type, payload, caller)), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? Saved(appended.Error, payload) : Unit.Value;
    }

    private static Error Saved(Error error, WorkflowChangedPayload payload) =>
        error with { Message = $"{error.Message} (the change itself is saved at revision {payload.Revision.ToString(CultureInfo.InvariantCulture)} of '{payload.Slug.Value}', but its event was not recorded; read the workflow before you retry)" };

    // why: a workflow is made and archived once, so its id keys those; a revision, an activation or a pause can repeat, so the command that asked keys them
    private static string Key(string type, WorkflowChangedPayload payload, CallerContext caller) =>
        string.Equals(type, EventTypes.WorkflowCreated, StringComparison.Ordinal) || string.Equals(type, EventTypes.WorkflowArchived, StringComparison.Ordinal) ? $"{type}:{payload.WorkflowId.Value}" : $"{type}:{caller.CommandId.Value.ToString("N", CultureInfo.InvariantCulture)}";
}
