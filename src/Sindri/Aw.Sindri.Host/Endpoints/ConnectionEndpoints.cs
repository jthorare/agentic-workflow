using Aw.Kernel.Services;
using Aw.Sindri.Features.AddConnection;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Features.RemoveConnection;
using Aw.Sindri.Features.TestConnection;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The connection routes (connections PR 10a): GET /sindri/connections and /{name}, POST /sindri/connections (add) and /{name}/test, DELETE /{name}.
/// Each binds, calls one service and maps the result. No route refuses an agent session (owner decision D2), and no answer holds a token.
/// </summary>
internal static class ConnectionEndpoints
{
    internal static RouteGroupBuilder MapConnectionReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/connections", static async (IService<ListConnectionsRequest, ListConnectionsResponse> service, CancellationToken ct)
            => (await service.Handle(new ListConnectionsRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/connections/{name}", static async (string name, IService<GetConnectionRequest, GetConnectionResponse> service, CancellationToken ct)
            => (await service.Handle(new GetConnectionRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    internal static RouteGroupBuilder MapConnectionWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/connections", static async (AddConnectionRequest request, IService<AddConnectionRequest, AddConnectionResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/connections/{name}/test", static async (string name, IService<TestConnectionRequest, TestConnectionResponse> service, CancellationToken ct)
            => (await service.Handle(new TestConnectionRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapDelete("/connections/{name}", static async (string name, IService<RemoveConnectionRequest, RemoveConnectionResponse> service, CancellationToken ct)
            => (await service.Handle(new RemoveConnectionRequest(name), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
