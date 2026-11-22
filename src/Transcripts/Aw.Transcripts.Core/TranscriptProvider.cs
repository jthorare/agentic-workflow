namespace Aw.Transcripts.Core;

/// <summary>The agent that wrote a transcript. Its wire name is <see cref="TranscriptProviderNames.Of"/>.</summary>
public enum TranscriptProvider
{
    /// <summary>Claude Code ('claude-code'): ~/.claude/projects.</summary>
    ClaudeCode,

    /// <summary>Codex ('codex'): ~/.codex/sessions.</summary>
    Codex,

    /// <summary>Cursor ('cursor'): ~/.cursor/projects.</summary>
    Cursor,
}
