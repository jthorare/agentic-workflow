using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>QUERY /sindri/context/pages/preview: reads a pasted page list (the JSON body {text, repo?}) against the map's pages and the concepts; writes nothing (pages spec §3.6).</summary>
internal static class PreviewContextPagesEndpoint
{
    internal static RouteGroupBuilder MapPreviewContextPages(this RouteGroupBuilder group)
    {
        _ = group.MapGroup(string.Empty).WithMetadata(BodyQueryRouteMetadata.Instance).MapMethods("/context/pages/preview", [HttpMethods.Query], static async (PreviewPagesRequest request, IService<PreviewPagesRequest, PagesPreview> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
