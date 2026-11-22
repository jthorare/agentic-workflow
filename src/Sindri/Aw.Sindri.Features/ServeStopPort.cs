using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Features;

/// <summary>
/// <see cref="IServeStop"/> over the stop handler itself, not its command chain: the restart that calls it is already audited and holds the
/// command gate, so the stop runs the way POST /sindri/stop does and records 'process.stopped' with the reason 'stop'.
/// </summary>
/// <param name="stop">The stop handler.</param>
internal sealed class ServeStopPort(StopProcessService stop) : IServeStop
{
    public async ValueTask<Result<ServeStopped>> Stop(CancellationToken cancellationToken)
    {
        var stopped = await stop.Handle(new StopProcessRequest(StopReason.Stop), cancellationToken).ConfigureAwait(false);
        return stopped.IsError ? stopped.Error : new ServeStopped(stopped.Value.Pid, stopped.Value.Epoch, stopped.Value.Sequence);
    }
}
