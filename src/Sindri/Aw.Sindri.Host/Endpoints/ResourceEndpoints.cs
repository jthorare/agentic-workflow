using Aw.Kernel.Services;
using Aw.Sindri.Features.ListResources;
using Aw.Sindri.Features.PurgeResources;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The resource routes (agent-first PR 14): GET /sindri/resources[?owner=] lists what is provisioned, POST /sindri/resources/purge '{owner}' deletes one owner's resources.
/// A purge is refused (SND-RES-003) while the owner's workflow is active or has a run in flight. No route refuses an agent session.
/// </summary>
internal static class ResourceEndpoints
{
    internal static RouteGroupBuilder MapResourceReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/resources", static async (string? owner, IService<ListResourcesRequest, ListResourcesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListResourcesRequest(owner), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    internal static RouteGroupBuilder MapResourceWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/resources/purge", static async (PurgeResourcesRequest request, IService<PurgeResourcesRequest, PurgeResourcesResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
