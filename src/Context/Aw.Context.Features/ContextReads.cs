using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Projects;
using Aw.Context.Features.GetProjects;
using Aw.Context.Features.MapHeads;
using Aw.Context.Features.RelinkPinAliases;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The small calls behind the facade that watchers and pages make: where the mapped repos stand, the stored projects and the pin-alias relink.</summary>
internal sealed class ContextReads(
    IService<MapHeadsRequest, IReadOnlyList<RepoHead>> heads,
    IService<GetProjectFactsRequest, ProjectFactsAnswer> projects,
    IService<RelinkPinAliasesRequest, RelinkPinAliasesResult> relink)
{
    public ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(ContextScope scope, CancellationToken cancellationToken) => heads.Handle(new MapHeadsRequest(scope), cancellationToken);

    public ValueTask<Result<RelinkPinAliasesResult>> RelinkPinAliases(ContextScope scope, CancellationToken cancellationToken) => relink.Handle(new RelinkPinAliasesRequest(scope), cancellationToken);

    public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken) =>
        projects.Handle(new GetProjectFactsRequest(request, scope), cancellationToken);
}
