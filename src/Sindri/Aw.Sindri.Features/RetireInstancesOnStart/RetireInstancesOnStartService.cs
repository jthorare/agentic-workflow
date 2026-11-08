using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RetireInstancesOnStart;

internal sealed class RetireInstancesOnStartService : IService<RetireInstancesOnStartRequest, RetireInstancesOnStartResponse>
{
    public ValueTask<Result<RetireInstancesOnStartResponse>> Handle(RetireInstancesOnStartRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
