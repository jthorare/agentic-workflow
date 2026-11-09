using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.PurgeResources;

/// <summary>Purges an owner's resources (stub until the real service lands in the next commit).</summary>
internal sealed class PurgeResourcesService : IService<PurgeResourcesRequest, PurgeResourcesResponse>
{
    public ValueTask<Result<PurgeResourcesResponse>> Handle(PurgeResourcesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
