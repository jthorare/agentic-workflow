using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ListLinearIssues;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// GET /sindri/linear/issues[?group=&amp;team=&amp;project=&amp;cycle=&amp;state=&amp;assignee=&amp;priority=&amp;label=&amp;after=&amp;limit=] (Linear hub spec §3; query): one page of the Work page's Linear list. A value outside its set
/// is SND-API-013. A session caller's Linear text (titles and the names inside the rows) is inside the untrusted fence; the human gets it plain.
/// </summary>
internal static class ListLinearIssuesEndpoint
{
    internal static RouteGroupBuilder MapListLinearIssues(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/linear/issues", static async Task<IResult> (string? group, string? team, string? project, string? cycle, string? state, string? assignee, string? priority, string? label, string? after, string? limit,
            IService<ListLinearIssuesRequest, ListLinearIssuesResponse> service, ICallerContextAccessor callers, ILinearIssueFencing fencing, CancellationToken ct) =>
        {
            var list = await service.Handle(new ListLinearIssuesRequest(group, team, project, cycle, state, assignee, priority, label, after, limit), ct).ConfigureAwait(false);
            return list.IsError || callers.Current?.Class != CallerClass.Session ? list.ToHttpResult() : fencing.List(list.Value);
        });
        return group;
    }
}
