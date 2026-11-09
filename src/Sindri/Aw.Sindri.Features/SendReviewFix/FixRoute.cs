using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>Who takes a fix task (PR review page spec §8.2 step 3, with the PR babysitter gone): a live session on the head branch, else nobody yet.</summary>
/// <param name="sessions">The live agent sessions, by checkout.</param>
internal sealed class FixRoute(IFixSessions sessions)
{
    internal const string Session = "session";
    internal const string Waiting = "waiting";

    /// <summary>Finds where the task goes.</summary>
    /// <param name="headRepo">The head repository as "owner/name", or null when unknown (no session can match).</param>
    /// <param name="branch">The head branch.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>The route and, for a session, its description.</returns>
    internal async ValueTask<(string Went, string? Session)> Find(string? headRepo, string? branch, CancellationToken cancellationToken)
    {
        if (headRepo is null || branch is null)
        {
            return (Waiting, null);
        }

        return await sessions.OnBranch(headRepo, branch, cancellationToken).ConfigureAwait(false) is { } session ? (Session, session) : (Waiting, null);
    }
}
