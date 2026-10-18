using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.GetRunSuspension;

internal sealed class GetRunSuspensionService(LaunchSessionEvents session, IRunSuspensions suspensions) : IService<GetRunSuspensionRequest, RunSuspensionView>
{
    public async ValueTask<Result<RunSuspensionView>> Handle(GetRunSuspensionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proven = session.Verify(request.LaunchId, request.Secret);
        if (proven.IsError)
        {
            return proven.Error;
        }

        var suspended = await suspensions.IsSuspended(request.LaunchId, cancellationToken).ConfigureAwait(false);
        return suspended.IsError ? suspended.Error : new RunSuspensionView(suspended.Value);
    }
}
