using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Features.ListResources;

/// <summary>Lists the provisioned resources, all or one owner's. A malformed owner is SND-RES-006; an owner with nothing lists none.</summary>
/// <param name="files">The resources on disk.</param>
internal sealed class ListResourcesService(IResourceFiles files) : IService<ListResourcesRequest, ListResourcesResponse>
{
    public ValueTask<Result<ListResourcesResponse>> Handle(ListResourcesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ResourceOwner? owner = null;
        if (!string.IsNullOrEmpty(request.Owner))
        {
            var parsed = ResourceOwner.Parse(request.Owner);
            if (parsed.IsError)
            {
                return ValueTask.FromResult<Result<ListResourcesResponse>>(parsed.Error);
            }

            owner = parsed.Value;
        }

        var listed = files.List(owner);
        Result<ListResourcesResponse> answer = listed.IsError
            ? listed.Error
            : new ListResourcesResponse([.. listed.Value.Select(static e => new ResourceView(e.Owner.ToString(), e.Kind.ToString().ToLowerInvariant(), e.Name, e.SizeBytes, e.Path))]);
        return ValueTask.FromResult(answer);
    }
}
