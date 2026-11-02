namespace Aw.Judge.Core.Transcripts;

/// <summary>One Edit, Write or MultiEdit change in a turn.</summary>
/// <param name="File">The file path.</param>
/// <param name="Before">The replaced text, empty for a Write.</param>
/// <param name="After">The new text.</param>
public sealed record TranscriptEdit(string File, string Before, string After);
