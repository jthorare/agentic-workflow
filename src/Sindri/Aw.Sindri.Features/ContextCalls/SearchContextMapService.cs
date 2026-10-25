using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Search;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The map search through the engine, for the caller's scope; the one service behind GET and QUERY.</summary>
internal sealed class SearchContextMapService(ContextScopes scopes, IContextEngine engine) : IService<SearchContextMapRequest, MapSearchView>
{
    public async ValueTask<Result<MapSearchView>> Handle(SearchContextMapRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Search(new MapSearchQuery(request.Q ?? string.Empty, request.Limit, request.Area, request.Repo, read.Value), scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
