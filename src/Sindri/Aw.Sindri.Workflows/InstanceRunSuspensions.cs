using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

internal sealed class InstanceRunSuspensions(IWorkflowInstances instances, IInstanceReader reader) : IRunSuspensions
{
    public ValueTask<Result<bool>> IsSuspended(LaunchId launch, CancellationToken cancellationToken)
    {
        _ = (instances, reader);
        throw new NotSupportedException("the suspension read is not built yet");
    }
}
