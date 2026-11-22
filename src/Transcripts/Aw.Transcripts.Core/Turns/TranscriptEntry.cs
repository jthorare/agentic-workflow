namespace Aw.Transcripts.Core.Turns;

/// <summary>One line a provider reader kept: tool results and bookkeeping lines are never entries.</summary>
/// <param name="Line">The 1-based line in the file.</param>
/// <param name="At">When the line was written (the reader falls back to the last time it saw, then the file's write time).</param>
/// <param name="Kind">What the line is.</param>
/// <param name="Text">The scrubbed text (empty for a machine prompt or a tool-only assistant line).</param>
/// <param name="Tools">An assistant line's tool names, in order; empty otherwise.</param>
public sealed record TranscriptEntry(int Line, DateTimeOffset At, EntryKind Kind, string Text, IReadOnlyList<string> Tools);
