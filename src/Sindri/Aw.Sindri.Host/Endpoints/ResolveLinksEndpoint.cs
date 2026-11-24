using Aw.Kernel.Services;
using Aw.Sindri.Features.ResolveLinks;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// QUERY /sindri/links/resolve (messages spec §8.2 "POST /sindri/links/resolve", §9.3, #3021): the JSON body {urls[]} (1 to 20) to one answer per url, a row
/// key and its fields or 'none'. A read with a body, so the QUERY method like the context body reads (the spec's POST would be a command; the route name was
/// api's to confirm). Writes nothing and calls no outside service.
/// </summary>
internal static class ResolveLinksEndpoint
{
    internal static RouteGroupBuilder MapResolveLinks(this RouteGroupBuilder group)
    {
        _ = group.MapGroup(string.Empty).WithMetadata(BodyQueryRouteMetadata.Instance).MapMethods("/links/resolve", [HttpMethods.Query], static async (ResolveLinksRequest request, IService<ResolveLinksRequest, ResolveLinksResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
