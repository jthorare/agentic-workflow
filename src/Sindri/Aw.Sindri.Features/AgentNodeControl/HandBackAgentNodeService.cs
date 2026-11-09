using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>
/// The human hands the 'run_agent' node back: its timeout resumes with what was left. Refused for an agent session, an unknown or ended run, a node the run is not waiting on and a node nobody holds (SND-GRAPH-110).
/// </summary>
/// <param name="runs">The runs.</param>
/// <param name="control">Asks the run which node it waits on and hands it back.</param>
/// <param name="callers">The caller.</param>
internal sealed class HandBackAgentNodeService(IWorkflowRunStore runs, IAgentNodeControl control, ICallerContextAccessor callers) : IService<HandBackAgentNodeRequest, AgentNodeControlled>
{
    public async ValueTask<Result<AgentNodeControlled>> Handle(HandBackAgentNodeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
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
