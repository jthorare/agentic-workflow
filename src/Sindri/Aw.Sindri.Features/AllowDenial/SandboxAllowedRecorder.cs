using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.AllowDenial;

/// <summary>Appends 'sandbox.allowed' under the current caller's actor, keyed by the command id (a retried command is the same write).</summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry; its actor is the caller's.</param>
internal sealed class SandboxAllowedRecorder(ILedgerStore ledger, EventFactory events)
{
    /// <summary>Appends the event.</summary>
    /// <param name="allowed">The payload.</param>
    /// <param name="grant">The lease this process holds.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>The ledger's error, or null when it is recorded.</returns>
    internal async ValueTask<Error?> Record(SandboxEntryAllowed allowed, LeaseGrant grant, CancellationToken cancellationToken)
    {
        var appended = await ledger.Append(events.Entry(EventTypes.SandboxAllowed, allowed, $"{EventTypes.SandboxAllowed}:{allowed.CommandId.Value}"), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : null;
    }
}
