using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ListProjects;

/// <summary>The Projects page (projects page spec §6.2): the owner state from views.db, Context's facts, merged by ProjectCards.</summary>
internal sealed class ListProjectsService : IService<ListProjectsRequest, ListProjectsResponse>
{
    public ValueTask<Result<ListProjectsResponse>> Handle(ListProjectsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Ph4: ListProjects");
}
