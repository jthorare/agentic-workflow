using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// The fields of Claude Code's own session record (~/.claude/sessions/&lt;pid&gt;.json) that the collector uses (collector spec §3, §4.1 step 3).
/// The file is undocumented and versioned by Claude Code, so nothing else is parsed: not the socket path, not the bridge id.
/// </summary>
/// <param name="Pid">The agent's process id.</param>
/// <param name="ProcStart">The agent's start ticks, which Claude stores as a JSON string.</param>
public sealed record ClaudeRecord(int Pid, string? ProcStart)
{
    /// <summary>The session id, or null when the record has none that fits the id pattern.</summary>
    public SessionId? Session { get; init; }

    /// <summary>The working directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>'interactive' for a person's session.</summary>
    public string? Kind { get; init; }

    /// <summary>'cli', or 'sdk-ts' for a T3 Code session.</summary>
    public string? Entrypoint { get; init; }

    /// <summary>The tmux place, '&lt;session&gt;:@&lt;window&gt;.%&lt;pane&gt;', with no socket.</summary>
    public string? Tmux { get; init; }

    /// <summary>True for a T3 Code session: an sdk-ts record under .t3/worktrees.</summary>
    public bool IsT3 => string.Equals(Entrypoint, "sdk-ts", StringComparison.Ordinal) && Cwd?.Contains("/.t3/worktrees", StringComparison.Ordinal) == true;

    /// <summary>The session's name; it may come from a prompt, so it is scrubbed before anyone sees it.</summary>
    public string? Name { get; init; }

    /// <summary>The record's own status word ('busy', 'idle', or one this build doesn't know).</summary>
    public string? Status { get; init; }

    /// <summary>When the status last changed.</summary>
    public DateTimeOffset? StatusUpdatedAt { get; init; }

    /// <summary>When the session started.</summary>
    public DateTimeOffset? StartedAt { get; init; }
}
