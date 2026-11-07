using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.GetLibraryDoc;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/library/{doc} (PR review page spec §6.1; query): one design doc. A session caller's title, path and error text are inside the untrusted fence; the human gets them plain.</summary>
internal static class GetLibraryDocEndpoint
{
    internal static RouteGroupBuilder MapGetLibraryDoc(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/library/{doc}", static async Task<IResult> (string doc, IService<GetLibraryDocRequest, GetLibraryDocResponse> service, ICallerContextAccessor callers, ILibraryDocFencing fencing, CancellationToken ct) =>
        {
            var answer = await service.Handle(new GetLibraryDocRequest(doc), ct).ConfigureAwait(false);
            return answer.IsError || callers.Current?.Class != CallerClass.Session ? answer.ToHttpResult() : fencing.Doc(answer.Value);
        });
        return group;
    }
}
