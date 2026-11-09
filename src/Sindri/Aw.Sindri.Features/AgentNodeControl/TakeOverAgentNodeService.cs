using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.AgentNodeControl;

/// <summary>Takes the 'run_agent' node over (red stub: the real service comes next).</summary>
internal sealed class TakeOverAgentNodeService : IService<TakeOverAgentNodeRequest, AgentNodeControlled>
{
    public TakeOverAgentNodeService(IWorkflowRunStore runs, IAgentNodeControl control, ICallerContextAccessor callers)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(callers);
    }

    public ValueTask<Result<AgentNodeControlled>> Handle(TakeOverAgentNodeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
