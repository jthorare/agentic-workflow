using Aw.Judge.Core.Ids;

namespace Aw.Judge.Core.Transcripts;

/// <summary>One real prompt and the edits the assistant made after it.</summary>
/// <param name="Session">The session; 'unknown' when a line carries none.</param>
/// <param name="Index">The turn's number in its session, from 0.</param>
/// <param name="Prompt">The prompt text.</param>
/// <param name="Edits">The edits, in order.</param>
public sealed record TranscriptTurn(SessionId Session, int Index, string Prompt, IReadOnlyList<TranscriptEdit> Edits);
