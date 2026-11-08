namespace Aw.Meta.Core.Lanes;

/// <summary>What the launcher recorded about the agent CLI a lane runs (the catalog entry's facts the meta needs).</summary>
/// <param name="Agent">The catalog id (claude-code, codex, cursor, or an added one).</param>
/// <param name="ProcessName">The process a pane runs while the agent is up.</param>
/// <param name="Hooks">Whether the CLI has hook support; false means the lane is launch-only.</param>
/// <param name="Transcripts">The transcript glob with {sessionId}; null when unknown.</param>
/// <param name="Codex">Whether the catalog entry is a Codex CLI (the built-in or an entry derived from it); a record from before this field is read by id and process name.</param>
/// <param name="NoDaemon">Whether the launcher started this Codex with --no-daemon; null when the record does not say.</param>
public sealed record LaneAgent(string Agent, string ProcessName, bool Hooks, string? Transcripts = null, bool Codex = false, bool? NoDaemon = null)
{
    /// <summary>The Claude Code agent id.</summary>
    public const string ClaudeCode = "claude-code";

    /// <summary>Whether this is a Codex CLI: the recorded flag, or the catalog id or process name, so an alias and an older record both count.</summary>
    public bool IsCodex => Codex || string.Equals(Agent, "codex", StringComparison.Ordinal) || string.Equals(ProcessName, "codex", StringComparison.Ordinal);

    /// <summary>Whether this is Claude Code, whose lanes use SendMessage and the transcript, not the lane commands.</summary>
    public bool IsClaude => string.Equals(Agent, ClaudeCode, StringComparison.Ordinal);
}
