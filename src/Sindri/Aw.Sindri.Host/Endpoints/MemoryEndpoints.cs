using Aw.Kernel.Services;
using Aw.Sindri.Features.CreateMemory;
using Aw.Sindri.Features.ForgetMemoryFact;
using Aw.Sindri.Features.ListMemories;
using Aw.Sindri.Features.SearchMemory;
using Aw.Sindri.Features.WriteMemoryFact;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// The memory routes (agent-first PR 13): GET /sindri/memories and /{name}/search, POST /sindri/memories (create) and /{name}/facts (write), DELETE /{name}/facts (forget).
/// Each binds, calls one service and maps the result. No route refuses an agent session.
/// </summary>
internal static class MemoryEndpoints
{
    internal static RouteGroupBuilder MapMemoryReads(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/memories", static async (IService<ListMemoriesRequest, ListMemoriesResponse> service, CancellationToken ct)
            => (await service.Handle(new ListMemoriesRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapGet("/memories/{name}/search", static async (string name, string query, string? level, int? limit, IService<SearchMemoryRequest, SearchMemoryResponse> service, CancellationToken ct)
            => (await service.Handle(new SearchMemoryRequest(name, query, level, limit), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    internal static RouteGroupBuilder MapMemoryWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/memories", static async (CreateMemoryRequest request, IService<CreateMemoryRequest, CreateMemoryResponse> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapPost("/memories/{name}/facts", static async (string name, WriteMemoryFactBody body, IService<WriteMemoryFactRequest, WriteMemoryFactResponse> service, CancellationToken ct)
            => (await service.Handle(new WriteMemoryFactRequest(name, body.Text, body.Tags, body.ExpiresAt, body.Provenance), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapDelete("/memories/{name}/facts", static async (string name, string? fact, string? tag, bool? expired, IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse> service, CancellationToken ct)
            => (await service.Handle(new ForgetMemoryFactRequest(name, fact, tag, expired == true), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
