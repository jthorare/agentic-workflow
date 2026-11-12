using Aw.Kernel.Services;
using Aw.Sindri.Features.RemoveSessionWorktree;
using Aw.Sindri.Features.StartSession;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// POST /sindri/sessions and DELETE /sindri/sessions/{actId}/worktree (agents spec §15.8, A7): start a one-off tmux session, and remove the worktree it made. The body of the POST is the request:
/// '{repo, agent, prompt?, worktree, actId}'. Human only: the catalog rows are desk rows without SessionOk, so a session caller gets SND-AUTH-002.
/// </summary>
internal static class SessionStartEndpoints
{
    internal static RouteGroupBuilder MapSessionStarts(this RouteGroupBuilder group)
    {
        _ = group.MapPost("/sessions", static async (StartSessionRequest request, IService<StartSessionRequest, StartedSession> service, CancellationToken ct)
            => (await service.Handle(request, ct).ConfigureAwait(false)).ToHttpResult());
        _ = group.MapDelete("/sessions/{actId}/worktree", static async (string actId, IService<RemoveSessionWorktreeRequest, RemovedWorktree> service, CancellationToken ct)
            => (await service.Handle(new RemoveSessionWorktreeRequest(actId), ct).ConfigureAwait(false)).ToHttpResult());
        return group;
    }
}
