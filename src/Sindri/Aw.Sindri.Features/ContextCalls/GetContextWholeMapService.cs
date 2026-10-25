using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.WholeMap;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The whole map of the repo the caller may read, through the engine (which gates and scopes sessions).</summary>
internal sealed class GetContextWholeMapService(ContextScopes scopes, IContextEngine engine, WholeMapNotReady notReady) : IService<GetContextWholeMapRequest, WholeMapView>
{
    public async ValueTask<Result<WholeMapView>> Handle(GetContextWholeMapRequest request, CancellationToken cancellationToken)
    {
        var read = MapRead.Of(request.Draft, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var whole = await engine.WholeMap(new WholeMapQuery(request.Detail, request.Locate ?? [], read.Value, request.Repo, request.Area) { Concept = request.Concept }, scope.Value, cancellationToken).ConfigureAwait(false);
        return whole.IsError && !request.Draft && request.MapVersion is null && string.Equals(whole.Error.Code, ContextErrors.NoMap.Code, StringComparison.Ordinal)
            ? await notReady.Explain(whole.Error, scope.Value, cancellationToken).ConfigureAwait(false)
            : whole;
    }
}
