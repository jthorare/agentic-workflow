using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetProject;

/// <summary>One project (projects page spec §6.2): the page built as ListProjects builds it (with every finished one), then the card the id names; SND-PROJECT-404 when none does.</summary>
internal sealed class GetProjectService : IService<GetProjectRequest, GetProjectResponse>
{
    public ValueTask<Result<GetProjectResponse>> Handle(GetProjectRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Ph4: GetProject");
}
