using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>Turns observed sessions into what a client sees, and decides who sees which (collector spec CA-1, CA-2, §12). Pure.</summary>
public static class SessionRows
{
    /// <summary>The live session named <paramref name="reference"/> in <paramref name="pass"/>, or null: an unknown or ended ref answers null.</summary>
    /// <param name="pass">One snapshot, read once by the caller.</param>
    /// <param name="reference">The ref.</param>
    /// <returns>The session, or null.</returns>
    public static ObservedSession? Find(SessionSnapshot pass, SessionRef reference)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return pass.Sessions.FirstOrDefault(s => s.Ref.Equals(reference));
    }

    /// <summary>The row of <paramref name="session"/>.</summary>
    /// <param name="session">The session.</param>
    /// <param name="entry">Its agent's catalog entry, or null when the catalog no longer has it.</param>
    /// <param name="repoPath">The checkout path of its repo in the approved profile, or null.</param>
    /// <param name="launch">The instance, Step and attempt of the session's Sindri launch, or null when none is known.</param>
    /// <returns>The row.</returns>
    public static SessionRow Of(ObservedSession session, AgentEntry? entry, string? repoPath, LaunchStep? launch = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        return new SessionRow(
            session.Ref.ToString(), session.Ref.Provider, entry?.Label ?? session.Ref.Provider.Value, session.Ref.Id, session.IdKnown, session.State, AskingOf(session), session.Source, session.Estimated,
            session.Repo, Worktree(session.Cwd, repoPath), HostOf(session.Host), LaneOf(session), session.LaunchId, session.StartedAt, session.Since, SessionFocus.Pending)
        {
            Title = session.Title,
            Pid = session.Pid,
            Launch = launch,
        };
    }

    private static SessionHostRow HostOf(HostInfo info)
    {
        var tmux = info.Tmux;
        return new SessionHostRow(info.Kind, info.Label, tmux?.SocketKind, tmux?.AttachedClients, info.Terminal?.Name)
        {
            Session = tmux?.Session,
            Pane = tmux?.Pane,
        };
    }

    private static SessionAsking? AskingOf(ObservedSession session) =>
        session.State == SessionState.Asking && session.Reason is { } reason ? new SessionAsking(reason, session.Tool, session.Since) : null;

    private static SessionLane? LaneOf(ObservedSession session) =>
        session.Team is { } team && session.Lane is { } lane ? new SessionLane(team, lane) : null;

    /// <summary>The same row with the tmux socket path added (the single-session read).</summary>
    /// <param name="row">The row.</param>
    /// <param name="session">The session it came from.</param>
    /// <returns>The row with <see cref="SessionHostRow.Socket"/> set.</returns>
    public static SessionRow WithSocket(SessionRow row, ObservedSession session)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(session);
        return row with { Host = row.Host with { Socket = session.Host.Tmux?.Socket } };
    }

    /// <summary>The argv that resumes <paramref name="session"/> from the entry's template, or null when there is no template or the id is not the agent's own.</summary>
    /// <param name="session">The session.</param>
    /// <param name="entry">Its agent's catalog entry, or null.</param>
    /// <returns>The argv, or null.</returns>
    public static IReadOnlyList<string>? Resume(ObservedSession session, AgentEntry? entry)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (entry?.Resume is not { Count: > 0 } template || !session.IdKnown)
        {
            return null;
        }

        return [.. template.Select(part => part.Replace("{bin}", entry.Bin, StringComparison.Ordinal).Replace("{sessionId}", session.Ref.Id.Value, StringComparison.Ordinal))];
    }

    /// <summary>The directory below the repo's checkout, or null when it is the checkout itself, outside it, or either is unknown.</summary>
    /// <param name="cwd">The session's directory.</param>
    /// <param name="repoPath">The checkout.</param>
    /// <returns>The repo-relative path.</returns>
    public static string? Worktree(string? cwd, string? repoPath)
    {
        if (cwd is null || repoPath is null)
        {
            return null;
        }

        var root = repoPath.TrimEnd('/') + "/";
        return cwd.StartsWith(root, StringComparison.Ordinal) && cwd.Length > root.Length ? cwd[root.Length..].TrimEnd('/') : null;
    }

    /// <summary>The unavailable error when the pass reports SND-SESSION-002 (collector spec CA-5: only these replace the list), else null.</summary>
    /// <param name="problems">The snapshot's problems.</param>
    /// <returns>The error, or null.</returns>
    public static Kernel.Error? Unavailable(IReadOnlyList<SessionProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.Select(static p => p.Reason).FirstOrDefault(static r => r is not null) is { } reason ? UnavailableReasons.ToError(reason) : null;
    }
}
