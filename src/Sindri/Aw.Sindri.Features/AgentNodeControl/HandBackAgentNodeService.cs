using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>
/// A person or an agent session hands the 'run_agent' node back: its timeout resumes with what was left. Refused for a request with no caller, an unknown or ended run, a node the run is not waiting on and a node nobody holds (SND-GRAPH-111).
/// </summary>
/// <param name="runs">The runs.</param>
/// <param name="control">Asks the run which node it waits on and hands it back.</param>
/// <param name="callers">The caller.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
internal sealed class HandBackAgentNodeService(IWorkflowRunStore runs, IAgentNodeControl control, ICallerContextAccessor callers, ILedgerStore ledger, HeldLease held, EventFactory events) : IService<HandBackAgentNodeRequest, AgentNodeControlled>
{
    public async ValueTask<Result<AgentNodeControlled>> Handle(HandBackAgentNodeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = (ledger, held, events);   // stub until the event is appended (#2497)
        var target = await AgentNodeTarget.Find(callers, runs, control, new AgentNodeAsk(request.Run, request.Node, request.ItemIndex), cancellationToken).ConfigureAwait(false);
        if (target.IsError)
        {
            return target.Error;
        }

        if (!target.Value.TakenOver)
        {
            return SindriErrors.GraphNodeNotTakenOver.With($"node '{request.Node.Value}' of run '{request.Run.Value}' is not taken over, so there is nothing to hand back");
        }

        var done = await control.Handback(request.Run, request.Node, request.ItemIndex, cancellationToken).ConfigureAwait(false);
        return done.IsError ? done.Error : new AgentNodeControlled(request.Run, request.Node, request.ItemIndex, AgentNodeTarget.Waiting);
    }
}
