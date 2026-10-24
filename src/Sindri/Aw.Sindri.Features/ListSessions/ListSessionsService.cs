using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.ListSessions;

/// <summary>
/// The live sessions (collector spec CA-1): the collector's snapshot, trimmed to the caller. A session caller sees only its repo's rows and none of the human-only fields;
/// the head counts cover what the caller may see, before the request's filters. SND-SESSION-002 replaces the list.
/// </summary>
internal sealed class ListSessionsService(ISessionSnapshot snapshot, IAgentCatalog catalog, IApprovedProfile approvals, ICallerContextAccessor callers, TimeProvider clock, ILaunchViews launches)
    : IService<ListSessionsRequest, SessionList>
{
    internal const string EmptyText = "No agent sessions are running.";

    public async ValueTask<Result<SessionList>> Handle(ListSessionsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the session list needs a caller");
        }

        if (snapshot.Current is not { } current)
        {
            return new SessionList([], [], [], new SessionCounts(0, 0, 0), EmptyText, clock.GetUtcNow());
        }

        if (SessionRows.Unavailable(current.Problems) is { } unavailable)
        {
            return unavailable;
        }

        var paths = await RepoPaths(cancellationToken).ConfigureAwait(false);
        var visible = current.Sessions;
        var matching = visible.Where(s => Matches(request, s)).ToList();
        var endedVisible = current.RecentlyEnded;
        var steps = await LaunchSteps.Of(launches, matching.Concat(endedVisible.Select(static e => e.Session)).Select(static s => s.LaunchId).OfType<LaunchId>(), cancellationToken).ConfigureAwait(false);
        var shown = matching.Select(s => Row(s, paths, steps)).ToList();
        var ended = endedVisible.Select(e => Ended(e, paths, steps)).ToList();
        var counts = new SessionCounts(visible.Count, visible.Count(static s => s.State == SessionState.Asking), visible.Count(static s => s.LaunchId is null));
        return new SessionList(shown, ended, current.Problems, counts, shown.Count == 0 ? EmptyText : null, current.ObservedAt);
    }

    private static bool Matches(ListSessionsRequest request, ObservedSession s) =>
        (request.Provider is not { } provider || s.Ref.Provider.Equals(provider))
        && (request.State is not { } state || s.State == state)
        && (request.Repo is not { } repo || Equals(s.Repo, repo))
        && (request.InWorkflow is not { } inWorkflow || (s.LaunchId is not null) == inWorkflow);

    private SessionRow Row(ObservedSession s, IReadOnlyDictionary<string, string> paths, IReadOnlyDictionary<LaunchId, LaunchStep> steps) =>
        SessionRows.Of(s, catalog.Resolve(s.Ref.Provider), s.Repo is { } repo ? paths.GetValueOrDefault(repo.Value) : null, s.LaunchId is { } launch ? steps.GetValueOrDefault(launch) : null);

    private EndedSessionRow Ended(EndedSession e, IReadOnlyDictionary<string, string> paths, IReadOnlyDictionary<LaunchId, LaunchStep> steps) =>
        new(Row(e.Session, paths, steps), e.EndedAt, SessionRows.Resume(e.Session, catalog.Resolve(e.Session.Ref.Provider)));

    private async ValueTask<IReadOnlyDictionary<string, string>> RepoPaths(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return approved.IsSuccess && approved.Value is { } loaded
            ? (loaded.Profile.Repos ?? []).GroupBy(static r => r.Name, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.First().Path, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
