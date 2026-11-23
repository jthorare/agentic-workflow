using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.Commands;
using Aw.Sindri.Features.RestartProcess;
using Aw.Sindri.Features.StopProcess;

namespace Aw.Sindri.Features;

/// <summary>
/// <see cref="IServeStop"/> over the stop handler itself, not its command chain: the restart that calls it holds the command gate, so the stop runs the
/// way POST /sindri/stop does and records 'process.stopped' with the reason 'stop'. The stop clears the lease grant an audit row needs, so first it writes
/// the restart's accepted 'command.handled' row while the grant is held (the RestartProcess row is self-audited). A row that can't be written stops the
/// restart: the port returns that error, serve keeps running, and the chain audits the failed attempt.
/// </summary>
/// <param name="stop">The stop handler.</param>
/// <param name="audit">Writes the restart's audit row.</param>
/// <param name="callers">The restart's caller.</param>
/// <param name="catalog">The command catalog the restart is classified by.</param>
internal sealed class ServeStopPort(StopProcessService stop, AuditTrail audit, ICallerContextAccessor callers, ICommandCatalog catalog) : IServeStop
{
    public async ValueTask<Result<ServeStopped>> Stop(CancellationToken cancellationToken)
    {
        var row = ActionClassifier.Classify(catalog, new RestartProcessRequest());
        var recorded = await audit.Record(callers.Current, row, CommandOutcome.Accepted, code: null, CancellationToken.None).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        var stopped = await stop.Handle(new StopProcessRequest(StopReason.Stop), cancellationToken).ConfigureAwait(false);
        return stopped.IsError ? stopped.Error : new ServeStopped(stopped.Value.Pid, stopped.Value.Epoch, stopped.Value.Sequence);
    }
}
