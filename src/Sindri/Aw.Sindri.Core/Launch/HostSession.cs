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

    /// <summary>The catalog agent the launch asked for, for a host that picks its own runtime for it (T3); null when not given.</summary>
    public AgentId? Agent { get; init; }
}
