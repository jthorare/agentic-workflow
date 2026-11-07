using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.LinearConnection;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Settings, Connections, Linear (Linear hub spec 2026-11-04 §3): the connection read, the team list for a pasted key, the key save and the write key's removal. Every route is the owner's: a session is SND-AUTH-002 (403).
/// A key refused by Linear answers 502 (Linear's answer, not ours), a keychain that took nothing 422.
/// </summary>
internal static class LinearConnectionEndpoints
{
    internal static RouteGroupBuilder MapGetLinearConnection(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/linear/connection", static async (IService<GetLinearConnectionRequest, LinearConnectionView> service, CancellationToken ct)
            => Answer(await service.Handle(new GetLinearConnectionRequest(), ct).ConfigureAwait(false)));
        return group;
    }

    internal static RouteGroupBuilder MapLinearConnectionWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/linear/connection/teams", static async (ListLinearTeamsRequest request, IService<ListLinearTeamsRequest, ListLinearTeamsResponse> service, CancellationToken ct)
            => Answer(await service.Handle(request, ct).ConfigureAwait(false)));
        _ = group.MapPost("/linear/connection", static async (SaveLinearConnectionRequest request, IService<SaveLinearConnectionRequest, SaveLinearConnectionResponse> service, CancellationToken ct)
            => Answer(await service.Handle(request, ct).ConfigureAwait(false)));
        _ = group.MapDelete("/linear/connection/write", static async (IService<RemoveLinearWriteKeyRequest, RemoveLinearWriteKeyResponse> service, CancellationToken ct)
            => Answer(await service.Handle(new RemoveLinearWriteKeyRequest(), ct).ConfigureAwait(false)));
        return group;
    }

    // why: the status follows who is wrong. Linear answered no or gibberish (502), or the keychain took nothing (422); the kinds' own statuses (400, 403, 503) are right for the rest
    private static IResult Answer<T>(Result<T> result) => result.IsSuccess ? result.ToHttpResult() : result.Error.Code switch
    {
        "SND-LINEAR-004" or "SND-CONTEXT-072" => HttpResults.Problem(result.Error, StatusCodes.Status502BadGateway),
        "SND-LINEAR-014" => HttpResults.Problem(result.Error, StatusCodes.Status422UnprocessableEntity),
        _ => result.ToHttpResult(),
    };
}
