using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.GetSession;

/// <summary>
/// Two facts a session row gets when it is read, not when the collector passes (Agents page, issue 3082): the branch its worktree has checked out (read from the worktree's
/// '.git' files, null when detached or unknown) and, for a session Sindri started that the agent has not named, the title the start made from its prompt.
/// </summary>
/// <param name="branches">Reads a worktree's branch.</param>
/// <param name="started">The starts of this process, by act id.</param>
internal sealed class SessionRowFacts(IBranchReader branches, IStartedSessions started)
{
    private const string Prefix = "sindri-";

    /// <summary>The row with its branch and title set.</summary>
    /// <param name="row">The row.</param>
    /// <param name="session">The session it came from.</param>
    /// <returns>The row.</returns>
    internal SessionRow With(SessionRow row, ObservedSession session)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(session);
        return row with { Branch = session.Cwd is { Length: > 0 } cwd ? branches.Branch(cwd) : null, Title = row.Title ?? StartedTitle(session) };
    }

    /// <summary>The tmux session of a start is 'sindri-&lt;actId&gt;'; the entry this process kept for that act id holds the title.</summary>
    private string? StartedTitle(ObservedSession session) =>
        session.Host.Tmux?.Session is { } name && name.StartsWith(Prefix, StringComparison.Ordinal) && ActId.TryFrom(name[Prefix.Length..], out var act)
            ? started.InMemory(act)?.Title
            : null;
}
