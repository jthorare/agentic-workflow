using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Graph;

/// <summary>
/// Writes the workflow lifecycle events ('workflow.created', 'workflow.revised', 'workflow.activated', 'workflow.paused', 'workflow.archived'). A write slice checks
/// <see cref="LeaseProblem"/> before it touches the store, so a refusal leaves the workflow as it was, then calls <see cref="Record"/> once the store accepted the change.
/// </summary>
/// <param name="ledger">The ledger.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
public sealed class WorkflowChangeLog(ILedgerStore ledger, HeldLease held, EventFactory events)
{
    /// <summary>Why no event can be written now.</summary>
    /// <returns>SND-API-001 when the control plane holds no lease, else null.</returns>
    public Error? LeaseProblem() => held.Current is null ? SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)") : null;

    /// <summary>Appends one lifecycle event.</summary>
    /// <param name="type">One of the five <see cref="EventTypes"/> workflow types.</param>
    /// <param name="payload">What changed.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the append failed.</returns>
    public async ValueTask<Result<Unit>> Record(string type, WorkflowChangedPayload payload, string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await ledger.Append(events.Entry(type, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
