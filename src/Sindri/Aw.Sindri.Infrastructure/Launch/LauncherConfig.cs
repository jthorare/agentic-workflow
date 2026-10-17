using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What launcher.json and the built-ins give the collector and the launcher.</summary>
/// <param name="Agents">The merged catalog, built-ins first.</param>
/// <param name="Sessions">The 'sessions' section, or null.</param>
internal sealed record LauncherConfig(IReadOnlyList<AgentEntry> Agents, SessionsSection? Sessions)
{
    /// <summary>What was wrong in the parts the collector reads (the file as a whole, 'agents' entries, 'sessions'): the sentence behind the launcher problem row. Null when all was well.</summary>
    public string? Problem { get; init; }

    /// <summary>What was wrong in 'viewer' or 'host': it only affects attended opens, so the collector never sees it. Each entry names its section.</summary>
    public IReadOnlyList<string> AttendedProblems { get; init; } = [];

    /// <summary>The entries dropped from <see cref="Agents"/>, each with its line.</summary>
    public IReadOnlyList<DroppedEntry> Dropped { get; init; } = [];

    /// <summary>The file couldn't be used at all (not JSON, not an object, schema isn't 1, or unreadable): attended opens are refused and Steps park with SND-LAUNCH-011.</summary>
    public bool Unusable { get; init; }

    /// <summary>The 1-based line of the parse error when the file isn't JSON, else 0.</summary>
    public int ProblemLine { get; init; }

    /// <summary>The file's <c lang="text">viewer</c> section when it is valid, else null (a bad one is in <see cref="AttendedProblems"/>).</summary>
    public ViewerSection? Viewer { get; init; }

    /// <summary>The file's <c lang="text">host</c> section when it is valid, else null.</summary>
    public HostSection? Host { get; init; }
}
