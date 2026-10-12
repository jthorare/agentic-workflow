using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.RecordHeartbeat;

internal sealed class RecordHeartbeatService(ILedgerStore ledger, EventFactory events, TimeProvider clock)
    : IService<RecordHeartbeatRequest, RecordHeartbeatResponse>
{
    public async ValueTask<Result<RecordHeartbeatResponse>> Handle(RecordHeartbeatRequest request, CancellationToken cancellationToken)
    {
        var run = request.Run;
        var type = EventTypes.ServiceRun(HeartbeatService.ServiceId);
        var entry = events.Entry(type, new Heartbeat(run.Slot, clock.GetUtcNow()),
            string.Create(CultureInfo.InvariantCulture, $"{type}:{run.Grant.Epoch}:{run.Slot:O}"));
        var recorded = await ledger.Append(entry, run.Grant, cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new RecordHeartbeatResponse(recorded.Value.Sequence, recorded.Value.AlreadyRecorded);
    }
}
