using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Features.PingProcess;

internal sealed class PingProcessService(HeldLease held, IProcessProbe probe) : IService<PingProcessRequest, Ping>
{
    public ValueTask<Result<Ping>> Handle(PingProcessRequest request, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<Ping>>(new Ping(probe.Self().Pid, held.Current?.Epoch ?? 0));
}
