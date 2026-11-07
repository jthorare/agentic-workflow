using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.GetLibrary;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/library (PR review page spec §6.1; query): the design library's state and its errors. A session caller's file paths and finding texts are inside the untrusted fence, and it sees no unreadable folders; the human gets it plain.</summary>
internal static class GetLibraryEndpoint
{
    internal static RouteGroupBuilder MapGetLibrary(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/library", static async Task<IResult> (IService<GetLibraryRequest, GetLibraryResponse> service, ICallerContextAccessor callers, IReviewFencing fencing, CancellationToken ct) =>
        {
            var library = await service.Handle(new GetLibraryRequest(), ct).ConfigureAwait(false);
            return library.IsError || callers.Current?.Class != CallerClass.Session ? library.ToHttpResult() : fencing.Library(library.Value);
        });
        return group;
    }
}
