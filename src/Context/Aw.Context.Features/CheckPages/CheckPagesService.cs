using Aw.Context.Core.Map;
using Aw.Context.Core.Pages;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckPages;

/// <summary>Doctor 'context:pages' (pages spec §3.7): warns for each page key or concept the current map no longer has, saying whether a deny rule hides it or it is gone, and for caps a hand-edited profile breaks. Reads the stored map without the pages laid over it.</summary>
internal sealed class CheckPagesService(IGraphStore store) : IService<CheckPagesRequest, MapHealth>
{
    public async ValueTask<Result<MapHealth>> Handle(CheckPagesRequest request, CancellationToken cancellationToken)
    {
        if (request.Scope is not { } scope || !scope.Repos.Any(r => r.Pages.Count > 0))
        {
            return PageHealth.NoPages;
        }

        var current = await store.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return Fail(current.Error);
        }

        if (current.Value is not { } version)
        {
            return PageHealth.NoMap;
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (graph.IsError || overrides.IsError)
        {
            return Fail(graph.IsError ? graph.Error : overrides.Error);
        }

        // The deny cut as every reader makes it, without the pages and without a session's scope: what is left is what a page may point at.
        var seen = MapAccess.View(graph.Value, overrides.Value, scope with { OmitPages = true }, pending: false, callerScope: false);
        return seen.IsError ? Fail(seen.Error) : PageHealth.Check(scope.Repos, graph.Value, seen.Value.Graph);
    }

    private static MapHealth Fail(Error error) =>
        new(PageHealth.Name, MapHealthState.Fail, $"{error.Code}: {error.Message}", "check $AW_SINDRI_HOME/context permissions; never delete context.db");
}
