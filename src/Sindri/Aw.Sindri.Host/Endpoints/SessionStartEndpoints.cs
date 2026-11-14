using Aw.Kernel.Services;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetLastStart;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.StartSession;
using Aw.Sindri.Features.StopSession;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/sessions, DELETE /sindri/sessions/{actId}/worktree and POST /sindri/sessions/{provider}/{id}/stop (agents spec §15.8, A7; issue 3147): start a one-off tmux session, remove the worktree it made, and end it. The body of the POST is the request:
/// '{repo, agent, prompt?, worktree, actId}'. Human only: the catalog rows are desk rows without SessionOk, so a session caller gets SND-AUTH-002.
/// </summary>
internal static class SessionStartEndpoints
{
    /// <summary>GET /sindri/sessions/last-start: the repo and agent of the last 2xx start, both null when none (agents spec §15.4). A read, so agent sessions may call it.</summary>
    internal static RouteGroupBuilder MapGetLastStart(this RouteGroupBuilder group)
    {
        _ = group.MapGet("/sessions/last-start", static async (IService<GetLastStartRequest, LastStartView> service, CancellationToken ct)
            => (await service.Handle(new GetLastStartRequest(), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }

    internal static RouteGroupBuilder MapSessionStarts(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sessions", static async (StartSessionRequest request, IService<StartSessionRequest, StartedSession> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapDelete("/sessions/{actId}/worktree", static async (string actId, IService<RemoveSessionWorktreeRequest, RemovedWorktree> service, CancellationToken ct)
            => ActId.TryFrom(actId, out var act)
                ? (await service.Handle(new RemoveSessionWorktreeRequest(act), ct).ConfigureAwait(false)).ToHttpResult()
                : HttpResults.Problem(Core.SindriErrors.InvalidInput.With("an act id is 1 to 64 characters")));
        _ = group.MapPost("/sessions/{provider}/{id}/stop", static async (string provider, string id, IService<StopSessionRequest, StoppedSession> service, CancellationToken ct) =>
        {
            var session = SessionRoute.Ref(provider, id);
            return session.IsError ? session.ToHttpResult() : (await service.Handle(new StopSessionRequest(session.Value), ct).ConfigureAwait(false)).ToHttpResult();
        });
        return group;
    }
}
