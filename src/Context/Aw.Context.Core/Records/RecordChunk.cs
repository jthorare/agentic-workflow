namespace Aw.Context.Core.Records;

/// <summary>One scrubbed slice of a record's text, the unit of lexical and dense search (spec §4.2).</summary>
/// <param name="Seq">Its position in the record, from 0, without gaps.</param>
/// <param name="Text">Its scrubbed text.</param>
/// <param name="Tokens">Its token count.</param>
/// <param name="TextHash">The sha-256 of <paramref name="Text"/>: what a vector's hash is compared with.</param>
/// <param name="LineStart">The first line of a doc chunk, or null.</param>
/// <param name="LineEnd">The last line of a doc chunk, or null.</param>
public sealed record RecordChunk(int Seq, string Text, int Tokens, string TextHash, int? LineStart = null, int? LineEnd = null);
