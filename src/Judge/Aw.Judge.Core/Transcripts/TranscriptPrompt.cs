namespace Aw.Judge.Core.Transcripts;

/// <summary>A real prompt in a transcript (machine text, tool results and compaction summaries are not).</summary>
/// <param name="At">When it was sent.</param>
/// <param name="Text">The prompt text.</param>
public sealed record TranscriptPrompt(DateTimeOffset At, string Text);
