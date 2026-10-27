using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Transcripts;

/// <summary>A Claude transcript (one .jsonl file under ~/.claude/projects) read for the outcome labels and the turn import.</summary>
/// <param name="ModifiedAt">When the file was last written.</param>
/// <param name="Prompts">Every real prompt, in order.</param>
/// <param name="Turns">Every turn, in order.</param>
/// <param name="SessionEnds">The time of each session's last line.</param>
public sealed record Transcript(DateTimeOffset ModifiedAt, IReadOnlyList<TranscriptPrompt> Prompts, IReadOnlyList<TranscriptTurn> Turns, IReadOnlyDictionary<SessionId, DateTimeOffset> SessionEnds);
