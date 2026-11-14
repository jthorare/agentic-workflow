namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The session the terminal view shows, as the page knows it (agents spec §15.3, §15.5).</summary>
internal sealed record TerminalTarget
{
    /// <summary>The row key; the screen cache keys on it.</summary>
    public required string Key { get; init; }

    /// <summary>The name in the header, "ideas".</summary>
    public required string Name { get; init; }

    /// <summary>Lane, listed or one-off.</summary>
    public required TerminalKind Kind { get; init; }

    /// <summary>The tmux socket kind the header shows: sindri, default or team. Empty for a host with no pane.</summary>
    public string SocketKind { get; init; } = string.Empty;

    /// <summary>False for a Warp tab, an IDE terminal or a plain terminal (§15.5).</summary>
    public bool CanAttach { get; init; } = true;

    /// <summary>The reason in words when <see cref="CanAttach"/> is false: "Warp tab: Sindri can't show or type into this terminal."</summary>
    public string CantAttachReason { get; init; } = string.Empty;

    /// <summary>The transcript's last lines, shown on the can't-attach card.</summary>
    public string TailText { get; init; } = string.Empty;

    /// <summary>True for a one-off whose agent has not yet written a hook state: the header says Starting.</summary>
    public bool IsStarting { get; init; }

    /// <summary>True when Sindri started the session (a one-off, or a launched session): only then do Stop and Interrupt show (#3023).</summary>
    public bool StartedBySindri { get; init; }

    /// <summary>The act id of a one-off session; empty for the others.</summary>
    public string ActId { get; init; } = string.Empty;

    /// <summary>The one-off session's worktree path; empty when it has none.</summary>
    public string WorktreePath { get; init; } = string.Empty;

    /// <summary>The command that resumes the session; empty when none is known.</summary>
    public string ResumeCommand { get; init; } = string.Empty;
}
