using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.ListSessions;

namespace Aw.Sindri.Host.Endpoints;

internal static class ListSessionsEndpoint
{
    internal static RouteGroupBuilder MapListSessions(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions", static async Task<IResult> (string? provider, string? state, string? repo, string? inWorkflow, IService<ListSessionsRequest, SessionList> service, ICallerContextAccessor callers, ISessionFencing fencing, CancellationToken ct) =>
        {
            var filter = SessionRoute.Provider(provider).Bind(p => SessionRoute.State(state)
                .Bind(s => ItemRoute.Repo(repo).Bind(r => SessionRoute.InWorkflow(inWorkflow).Map(w => new ListSessionsRequest(p, s, r, w)))));
            if (filter.IsError)
            {
                return filter.ToHttpResult();
            }

            var list = await service.Handle(filter.Value, ct).ConfigureAwait(false);
            return list.IsError || callers.Current?.Class != CallerClass.Session ? list.ToHttpResult() : fencing.List(list.Value);   // a session reads pane text inside the untrusted fence
        });
        return group;
    }
}
