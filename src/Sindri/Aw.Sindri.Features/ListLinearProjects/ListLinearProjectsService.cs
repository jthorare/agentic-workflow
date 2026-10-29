using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListLinearProjects;

/// <summary>The From Linear tab (projects page spec §3.2, §6.2): the catalog minus what the owner leads, has attached or is finished.</summary>
internal sealed class ListLinearProjectsService : IService<ListLinearProjectsRequest, ListLinearProjectsResponse>
{
    public ValueTask<Result<ListLinearProjectsResponse>> Handle(ListLinearProjectsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
