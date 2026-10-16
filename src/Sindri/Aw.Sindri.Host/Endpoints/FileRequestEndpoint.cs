using Aw.Kernel.Services;
using Aw.Sindri.Features.FileRequest;

namespace Aw.Sindri.Host.Endpoints;

internal static class FileRequestEndpoint
{
    internal static RouteGroupBuilder MapFileRequest(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/requests", static async (FileRequestRequest request, IService<FileRequestRequest, RequestFiledResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
