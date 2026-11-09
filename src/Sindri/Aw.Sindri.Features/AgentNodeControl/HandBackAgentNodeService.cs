using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>Hands the 'run_agent' node back (red stub: the real service comes next).</summary>
internal sealed class HandBackAgentNodeService : IService<HandBackAgentNodeRequest, AgentNodeControlled>
{
    public HandBackAgentNodeService(IWorkflowRunStore runs, IAgentNodeControl control, ICallerContextAccessor callers)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(callers);
    }

    public ValueTask<Result<AgentNodeControlled>> Handle(HandBackAgentNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
