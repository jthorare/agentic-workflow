using Aw.Context.Core.Concepts;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/pins/suggest?concept=|search=|line=&amp;def=&amp;limit=&amp;repo=: a concept's terms with the records each would newly link, the features a search finds, or the concept each pasted line matches.</summary>
internal static class GetContextPinSuggestEndpoint
{
    internal static RouteGroupBuilder MapGetContextPinSuggest(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/pins/suggest", static async (string? concept, string? search, string[]? line, string[]? def, int? limit, string? repo, IService<GetContextPinSuggestRequest, PinSuggestView> service, CancellationToken ct)
            => (await service.Handle(new GetContextPinSuggestRequest(concept, search, line, def, limit, repo), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
