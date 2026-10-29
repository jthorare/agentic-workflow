using Aw.Context.Core.Engine;
using Aw.Context.Core.Projects;
using Aw.Context.Features.GetProjects;
using Aw.Context.Features.MapHeads;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The small reads behind the facade that watchers and pages call: where the mapped repos stand, and the stored projects.</summary>
internal sealed class ContextReads(
    IService<MapHeadsRequest, IReadOnlyList<RepoHead>> heads,
    IService<GetProjectFactsRequest, ProjectFactsAnswer> projects)
{
    public ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(ContextScope scope, CancellationToken cancellationToken) => heads.Handle(new MapHeadsRequest(scope), cancellationToken);

    public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken) =>
        projects.Handle(new GetProjectFactsRequest(request, scope), cancellationToken);
}
