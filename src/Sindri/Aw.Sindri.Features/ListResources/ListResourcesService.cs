using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListResources;

/// <summary>Lists the provisioned resources (stub until the real service lands in the next commit).</summary>
internal sealed class ListResourcesService : IService<ListResourcesRequest, ListResourcesResponse>
{
    public ValueTask<Result<ListResourcesResponse>> Handle(ListResourcesRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
