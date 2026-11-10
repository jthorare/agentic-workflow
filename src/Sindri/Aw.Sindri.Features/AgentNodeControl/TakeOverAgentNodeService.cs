using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>
/// A person or an agent session takes the 'run_agent' node a run waits on: its timeout pauses and only the human reports it. Refused for a request with no caller, an unknown or ended run and a node the run is not waiting on;
/// a node already held answers 'taken_over' again. The workflow alone holds the node's state, so nothing is written here.
/// </summary>
/// <param name="runs">The runs.</param>
/// <param name="control">Asks the run which node it waits on and takes it over.</param>
/// <param name="callers">The caller.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class TakeOverAgentNodeService(IWorkflowRunStore runs, IAgentNodeControl control, ICallerContextAccessor callers, ILedgerStore ledger, HeldLease held, EventFactory events) : IService<TakeOverAgentNodeRequest, AgentNodeControlled>
{
    public async ValueTask<Result<AgentNodeControlled>> Handle(TakeOverAgentNodeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = (ledger, held, events);   // stub until the event is appended (#2497)
        var target = await AgentNodeTarget.Find(callers, runs, control, new AgentNodeAsk(request.Run, request.Node, request.ItemIndex), cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return target.Error;
        }

        var done = await control.Takeover(request.Run, request.Node, request.ItemIndex, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : new AgentNodeControlled(request.Run, request.Node, request.ItemIndex, AgentNodeTarget.TakenOver);
    }
}
