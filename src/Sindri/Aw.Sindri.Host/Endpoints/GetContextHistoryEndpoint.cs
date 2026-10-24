using Aw.Context.Core.History;
using Aw.Kernel.Services;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>GET /sindri/context/history?anchor=&amp;since=&amp;before=&amp;draft=&amp;mapVersion=: the stored records, decisions and landed changes behind one or more anchors; a repeated 'anchor' names several.</summary>
internal static class GetContextHistoryEndpoint
{
    internal static RouteGroupBuilder MapGetContextHistory(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/context/history", static async (string[]? anchor, string? since, string? before, bool? draft, string? mapVersion, IService<GetContextHistoryRequest, HistoryView> history, CancellationToken ct) =>
        {
            var answer = await history.Handle(new GetContextHistoryRequest(anchor ?? [], since, before, draft ?? false, mapVersion), ct).ConfigureAwait(false);
            return answer.IsError ? HttpResults.Problem(answer.Error) : TypedResults.Ok(answer.Value);
        });
        return group;
    }
}
