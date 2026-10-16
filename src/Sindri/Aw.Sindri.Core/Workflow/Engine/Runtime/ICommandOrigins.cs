using Aw.Kernel;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// Which instance a reply's command was written for (port; engine spec rev 8 §5.4): the event bridge routes a 'route:
/// causation' event only to the instance whose command caused it. The command is the ledger event the reply names as its
/// causation; the adapter reads the instance from that event's own payload ('instance', as every 'workflow.*' event
/// the interpreter mirrors has). That read is the design: the effect's rows are keyed under the instance's address, so no
/// separate 'workflow_commands' map is kept.
/// </summary>
public interface ICommandOrigins
{
    /// <summary>The instance the command that caused <paramref name="reply"/> was written for.</summary>
    /// <param name="reply">The event; its envelope's causation id names the command.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The instance, or null when the reply names no causation, no recorded event has that id, or it names no instance.</returns>
    public ValueTask<Result<InstanceAddress?>> CauserOf(LedgerEvent reply, CancellationToken cancellationToken);
}
