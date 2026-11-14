using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Features.GetSession;

namespace Aw.Sindri.Host.Endpoints;

internal static class GetSessionEndpoint
{
    internal static RouteGroupBuilder MapGetSession(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions/{provider}/{id}", static async Task<IResult> (string provider, string id, IService<GetSessionRequest, SessionView> service, ICallerContextAccessor callers, ISessionFencing fencing, CancellationToken ct) =>
        {
            var session = SessionRoute.Ref(provider, id);
            if (session.IsError)
            {
                return session.ToHttpResult();
            }

            var view = await service.Handle(new GetSessionRequest(session.Value), ct).ConfigureAwait(false);
            return view.IsError || callers.Current?.Class != CallerClass.Session ? view.ToHttpResult() : fencing.One(view.Value);
        });
        return group;
    }
}
