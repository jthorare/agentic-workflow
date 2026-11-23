using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetSetup;

/// <summary>The setup list without the control plane (first-run spec decision 8): the answer has the shape GET /sindri/setup answers.</summary>
internal sealed class GetSetupOfflineService : IService<GetSetupOfflineRequest, GetSetupResponse>
{
    public ValueTask<Result<GetSetupResponse>> Handle(GetSetupOfflineRequest request, CancellationToken cancellationToken) => throw new NotSupportedException("red: P3 builds the setup list without serve");
}
