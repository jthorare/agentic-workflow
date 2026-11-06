using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Ports;

/// <summary>
/// The repos each agent session edited files in ('judge/trait-check/sessions/&lt;sid&gt;.repos'), so that the Stop check
/// judges a repo the session reached by absolute path, not only the one it runs in. Best effort: a record that can't be
/// written or read, or a session id that can't be a file name, is no repos, never an error.
/// </summary>
public interface ITraitCheckSessions
{
    /// <summary>Records that the session edited a file in the repo (once per repo).</summary>
    /// <param name="sessionId">The session's id; one that can't be a file name records nothing.</param>
    /// <param name="repoRoot">The repo's top-level directory, as git prints it.</param>
    public void Add(SessionId sessionId, string repoRoot);

    /// <summary>The repos the session edited files in, in the order it first did.</summary>
    /// <param name="sessionId">The session's id.</param>
    /// <returns>The repos' top-level directories; empty for a session with no record.</returns>
    public IReadOnlyList<string> Repos(SessionId sessionId);
}
