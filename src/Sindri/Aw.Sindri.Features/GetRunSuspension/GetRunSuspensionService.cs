using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetRunSuspension;

internal sealed class GetRunSuspensionService(LaunchSessionEvents session, IRunSuspensions suspensions) : IService<GetRunSuspensionRequest, RunSuspensionView>
{
    public ValueTask<Result<RunSuspensionView>> Handle(GetRunSuspensionRequest request, CancellationToken cancellationToken)
    {
        _ = (session, suspensions);
        throw new NotSupportedException("the suspension read is not built yet");
    }
}
