using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>One live session in a pass (collector spec §2, §7 CA-1). It never holds the environment, the transcript's content or the socket path.</summary>
/// <param name="Ref">Its name, '&lt;provider&gt;/&lt;id&gt;'.</param>
/// <param name="Pid">The agent's process id.</param>
/// <param name="StartTicks">The agent's start ticks: with the pid, the process's identity.</param>
/// <param name="State">What it is doing.</param>
/// <param name="Source">Where the state came from.</param>
/// <param name="Since">When the state last changed (as far as the source says).</param>
public sealed record ObservedSession(SessionRef Ref, int Pid, long StartTicks, SessionState State, StateSource Source, DateTimeOffset Since)
{
    /// <summary>True when the id is the agent's own, false for a 'pid-…' id.</summary>
    public bool IdKnown { get; init; }

    /// <summary>Why it is asking.</summary>
    public AskingReason? Reason { get; init; }

    /// <summary>The tool an approval is for.</summary>
    public string? Tool { get; init; }

    /// <summary>True when the state is a guess from the transcript's age.</summary>
    public bool Estimated { get; init; }

    /// <summary>The working directory (human callers only).</summary>
    public string? Cwd { get; init; }

    /// <summary>The profile repo it works in, once resolved.</summary>
    public RepoName? Repo { get; init; }

    /// <summary>The session's title, scrubbed and capped (human callers only).</summary>
    public string? Title { get; init; }

    /// <summary>What owns and shows it.</summary>
    public HostInfo Host { get; init; } = HostInfo.Unknown;

    /// <summary>The Sindri launch it belongs to, from AW_SINDRI_LAUNCH.</summary>
    public LaunchId? LaunchId { get; init; }

    /// <summary>The Meta team of its lane, from AW_META_TEAM.</summary>
    public string? Team { get; init; }

    /// <summary>The Meta lane, from AW_META_LANE.</summary>
    public string? Lane { get; init; }

    /// <summary>When the session started.</summary>
    public DateTimeOffset? StartedAt { get; init; }
}
