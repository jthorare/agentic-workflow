using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>Who takes a fix task (PR review page spec §8.2 step 3): a babysitter watching the PR, else a live session on the head branch, else nobody yet.</summary>
/// <param name="babysitters">The live babysitter instances' pull requests.</param>
/// <param name="sessions">The live agent sessions, by checkout.</param>
internal sealed class FixRoute(IFixBabysitters babysitters, IFixSessions sessions)
{
    internal const string Babysitter = "babysitter";
    internal const string Session = "session";
    internal const string Waiting = "waiting";

    /// <summary>Finds where the task goes.</summary>
    /// <param name="link">The PR's link.</param>
    /// <param name="headRepo">The head repository as "owner/name".</param>
    /// <param name="branch">The head branch.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The route and, for a session, its description.</returns>
    internal async ValueTask<(string Went, string? Session)> Find(string link, string headRepo, string branch, CancellationToken cancellationToken)
    {
        if (await babysitters.Watching(link, cancellationToken).ConfigureAwait(false))
        {
            return (Babysitter, null);
        }

        return await sessions.OnBranch(headRepo, branch, cancellationToken).ConfigureAwait(false) is { } session ? (Session, session) : (Waiting, null);
    }
}
