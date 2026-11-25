namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One human turn's text as 'transcript_batch' reads it.</summary>
/// <param name="Ref">The turn's ref.</param>
/// <param name="Text">The scrubbed text the person typed.</param>
/// <param name="Before">The scrubbed text of the assistant turn just before it, or null when there is none or it was not asked for.</param>
public sealed record TranscriptTurnText(string Ref, string Text, string? Before);
