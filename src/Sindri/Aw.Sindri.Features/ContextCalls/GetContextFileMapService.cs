using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The direct children of a repo path with their status, through the engine; a repo name that can't be one is SND-CONTEXT-045 like any repo outside the scope.</summary>
internal sealed class GetContextFileMapService(ContextScopes scopes, IContextEngine engine) : IService<GetContextFileMapRequest, FileMapView>
{
    public async ValueTask<Result<FileMapView>> Handle(GetContextFileMapRequest request, CancellationToken cancellationToken)
    {
        if (!ContextRepoName.TryFrom(request.Repo, out var repo))
        {
            return ContextErrors.RepoOutsideScope.With($"'{request.Repo}' is not a repo this scope can see (fix: name a repo of the profile)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError
            ? scope.Error
            : await engine.FileMap(new FileMapQuery(repo, request.Path ?? string.Empty, request.Unmapped, request.Draft, request.Cursor, request.Limit), scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
