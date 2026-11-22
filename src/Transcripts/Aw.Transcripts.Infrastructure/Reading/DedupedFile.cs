using Aw.Transcripts.Core.Turns;

namespace Aw.Transcripts.Infrastructure.Reading;

/// <summary>A file's read with its copied turns taken out.</summary>
/// <param name="Parsed">What the read found (its entries are cleared once the turns are built).</param>
/// <param name="Turns">The file's own turns, when they were asked for; empty otherwise.</param>
/// <param name="HumanTurns">How many of the file's own turns are human.</param>
internal sealed record DedupedFile(ParsedFile Parsed, IReadOnlyList<TranscriptTurn> Turns, int HumanTurns);
