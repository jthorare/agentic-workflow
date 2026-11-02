namespace Aw.Context.Core.Chunking;

/// <summary>One window of a record's clean text: its position, text, token estimate and line span (from 1, inclusive).</summary>
/// <param name="Seq">Its place among the record's chunks, from 0 with no gaps.</param>
/// <param name="Text">Its text, as it stands in the source.</param>
/// <param name="Tokens">Its token estimate (<see cref="Chunker.Estimate(int, int)"/>).</param>
/// <param name="LineStart">The source line its first character is on.</param>
/// <param name="LineEnd">The source line its last character is on.</param>
public sealed record TextChunk(int Seq, string Text, int Tokens, int LineStart, int LineEnd);
