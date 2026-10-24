using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSession;

/// <summary>
/// One live session (collector spec CA-2). A ref that is unknown or ended answers SND-SESSION-404. Every caller gets the directory, the socket and the state history.
/// </summary>
internal sealed class GetSessionService(ISessionSnapshot snapshot, IAgentCatalog catalog, IApprovedProfile approvals, ICallerContextAccessor callers, ILaunchViews launches)
    : IService<GetSessionRequest, SessionView>
{
    public async ValueTask<Result<SessionView>> Handle(GetSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a session needs a caller");
        }

        var now = snapshot.Current;   // one read: the check, the row and the history all come from the same pass
        if (now is not null && SessionRows.Unavailable(now.Problems) is { } unavailable)
        {
            return unavailable;
        }

        var found = now is null ? null : SessionRows.Find(now, request.Ref);
        if (now is null || found is null)
        {
            return SindriErrors.SessionNotFound.With("no live session has that ref");
        }

        var path = await RepoPath(found, cancellationToken).ConfigureAwait(false);
        var row = await Row(found, path, cancellationToken).ConfigureAwait(false);
        return new SessionView(SessionRows.WithSocket(row, found), found.Cwd, now.History.GetValueOrDefault(found.Ref) ?? [], now.ObservedAt);
    }

    private async ValueTask<SessionRow> Row(ObservedSession session, string? path, CancellationToken cancellationToken)
    {
        var steps = await LaunchSteps.Of(launches, session.LaunchId is { } id ? [id] : [], cancellationToken).ConfigureAwait(false);
        return SessionRows.Of(session, catalog.Resolve(session.Ref.Provider), path, session.LaunchId is { } launch ? steps.GetValueOrDefault(launch) : null);
    }

    private async ValueTask<string?> RepoPath(ObservedSession session, CancellationToken cancellationToken)
    {
        if (session.Repo is not { } repo)
        {
            return null;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return approved.IsSuccess ? approved.Value?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, repo.Value, StringComparison.Ordinal))?.Path : null;
    }
}
