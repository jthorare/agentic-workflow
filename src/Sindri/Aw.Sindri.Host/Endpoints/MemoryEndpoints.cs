using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
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
        _ = group.MapGet("/memories", static async Task<IResult> (IService<ListMemoriesRequest, ListMemoriesResponse> service, ICallerContextAccessor callers, IMemoryFencing fencing, CancellationToken ct)
            => Answer(await service.Handle(new ListMemoriesRequest(), ct).ConfigureAwait(false), callers, fencing.Stores));
        _ = group.MapGet("/memories/{name}/search", static async Task<IResult> (string name, string query, string? level, int? limit, IService<SearchMemoryRequest, SearchMemoryResponse> service, ICallerContextAccessor callers, IMemoryFencing fencing, CancellationToken ct)
            => Answer(await service.Handle(new SearchMemoryRequest(name, query, level, limit), ct).ConfigureAwait(false), callers, fencing.Facts));
        return group;
    }

    internal static RouteGroupBuilder MapMemoryWrites(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/memories", static async Task<IResult> (CreateMemoryRequest request, IService<CreateMemoryRequest, CreateMemoryResponse> service, ICallerContextAccessor callers, IMemoryFencing fencing, CancellationToken ct)
            => Answer(await service.Handle(request, ct).ConfigureAwait(false), callers, fencing.Store));
        _ = group.MapPost("/memories/{name}/facts", static async (string name, WriteMemoryFactBody body, IService<WriteMemoryFactRequest, WriteMemoryFactResponse> service, CancellationToken ct)
            => (await service.Handle(new WriteMemoryFactRequest(name, body.Text, body.Tags, body.ExpiresAt, body.Provenance), ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapDelete("/memories/{name}/facts", static async (string name, string? fact, string? tag, bool? expired, IService<ForgetMemoryFactRequest, ForgetMemoryFactResponse> service, CancellationToken ct)
            => (await service.Handle(new ForgetMemoryFactRequest(name, fact, tag, expired == true), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    /// <summary>The answer for the caller: a session gets it through the fence, the owner plain (as the Projects reads do).</summary>
    /// <param name="result">The service's result.</param>
    /// <param name="callers">Who is asking.</param>
    /// <param name="fence">Writes the answer fenced.</param>
    /// <typeparam name="T">The answer's type.</typeparam>
    /// <returns>The HTTP result.</returns>
    internal static IResult Answer<T>(Aw.Kernel.Result<T> result, ICallerContextAccessor callers, Func<T, IResult> fence)
    {
        ArgumentNullException.ThrowIfNull(callers);
        ArgumentNullException.ThrowIfNull(fence);
        return result.IsError || callers.Current?.Class != CallerClass.Session ? result.ToHttpResult() : fence(result.Value);
    }
}
