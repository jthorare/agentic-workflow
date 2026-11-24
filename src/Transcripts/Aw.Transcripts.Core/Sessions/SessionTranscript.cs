using Aw.Transcripts.Core.Turns;

namespace Aw.Transcripts.Core.Sessions;

/// <summary>One session's turns.</summary>
/// <param name="Session">The session.</param>
/// <param name="Turns">Its turns in file order, without the ones it copied from an earlier file.</param>
/// <param name="SkippedLines">Lines that were not a JSON object of the provider's shape and were skipped.</param>
public sealed record SessionTranscript(TranscriptSession Session, IReadOnlyList<TranscriptTurn> Turns, int SkippedLines);
