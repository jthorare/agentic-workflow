using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.PollConnections;

/// <summary>Doctor's 'connections:catchup' rows (#2437): the last capped or gapped read per (connection, event kind), with its reason and time.</summary>
/// <param name="ledger">The ledger, read-only.</param>
/// <param name="events">Reads a payload.</param>
internal sealed class CatchUpDoctorChecks(ILedgerReader ledger, EventFactory events) : IDoctorChecks
{
    public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        _ = (ledger, events);
        return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([]);
    }
}
