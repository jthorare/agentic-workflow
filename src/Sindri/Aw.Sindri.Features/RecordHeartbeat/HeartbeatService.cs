using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Features.RecordHeartbeat;

/// <summary>The heartbeat: S1's one background service, proving the scheduler. It records 'service.heartbeat' per slot.</summary>
internal sealed class HeartbeatService(IService<RecordHeartbeatRequest, RecordHeartbeatResponse> record) : IScheduledService
{
    public static readonly ServiceName ServiceId = ServiceName.From("heartbeat");

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<Unit>> Run(ScheduledRun run, CancellationToken cancellationToken) =>
        (await record.Handle(new RecordHeartbeatRequest(run), cancellationToken).ConfigureAwait(false)).Map(_ => Unit.Value);
}
