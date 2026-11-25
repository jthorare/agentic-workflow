namespace Aw.Sindri.Core.Launch;

/// <summary>What <see cref="ISessionHost.Start"/> runs.</summary>
/// <param name="Name">The host's session name ('snd-&lt;launchId&gt;').</param>
/// <param name="WorkDir">The directory the agent starts in.</param>
/// <param name="Argv">The command line; the first element is the executable ('sindri session wrap … -- claude …').</param>
/// <param name="Env">The environment the session gets, set through the host ('new-session -e'), never on a command line.</param>
public sealed record HostSession(string Name, string WorkDir, IReadOnlyList<string> Argv, IReadOnlyDictionary<string, string> Env)
{
    /// <summary>The repo checkout the launch belongs to, for a host that runs the agent itself and has to find the repo's project (T3); null when not given.</summary>
    public string? RepoPath { get; init; }

    /// <summary>The first message for a host that runs the agent itself (T3); a process host passes it in <see cref="Argv"/> instead.</summary>
    public string? Seed { get; init; }

    /// <summary>The session's title ('sindri ITM-…'), for a host that shows one; null when not given.</summary>
    public string? Title { get; init; }

    /// <summary>The status line a host that has one shows for this session, with the tmux prefix left as '&lt;prefix&gt;' for the host to fill in (a sandboxed session, sandbox spec §7); null for a session with none.</summary>
    public string? StatusRight { get; init; }

    /// <summary>The catalog agent the launch asked for, for a host that picks its own runtime for it (T3); null when not given.</summary>
    public AgentId? Agent { get; init; }

    /// <summary>The window size for a host that takes one (tmux 'new-session -x W -y H'); null leaves the host's default.</summary>
    public HostSize? Size { get; init; }

    /// <summary>True keeps the pane when the agent exits ('set-option remain-on-exit on'), so the view can show Ended (agents spec §15.2).</summary>
    public bool RemainOnExit { get; init; }

    /// <summary>True runs the command in a clean environment: only <see cref="Env"/> plus TERM reaches it ('env -i'), so nothing the tmux server inherited from Sindri's own process (every AW_ variable, GH_TOKEN) does (agents spec §15.4).</summary>
    public bool CleanEnvironment { get; init; }
}
