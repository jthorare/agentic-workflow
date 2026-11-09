using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The ledger side of <see cref="ILinearWriteRecorder"/>. The key is the client id for a create or a comment (a retry with the same id is the same write, so the event is recorded once) and the issue plus the command id for a change.
/// </summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry; its actor is the current caller's.</param>
/// <param name="callers">The caller: the command id.</param>
internal sealed class LedgerLinearWriteRecorder(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers) : ILinearWriteRecorder
{
    public async ValueTask<Error?> Record(string type, string team, string? issue, string? clientId, string? fields, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var key = clientId is not null ? $"{type}:{clientId}" : $"{type}:{issue}:{callers.Current?.CommandId.Value}";
        var appended = await ledger.Append(events.Entry(type, new LinearWriteRecorded(team, issue, fields), key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : null;
    }
}
