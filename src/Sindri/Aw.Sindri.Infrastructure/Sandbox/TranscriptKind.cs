namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Which CLI wrote the transcript, so the right reader parses it.</summary>
internal enum TranscriptKind
{
    /// <summary>A Claude Code session's jsonl.</summary>
    Claude,

    /// <summary>A Codex rollout.</summary>
    Codex,
}
