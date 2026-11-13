using System.Text.Json.Serialization;

namespace Aw.Context.Core.Results;

/// <summary>How many records matched and which of them the answer read (#3370): "268 matched, 25 read from rank 1". Only on an answer asked with a window.</summary>
/// <param name="Matched">Every record the sources matched, ranked.</param>
/// <param name="Offset">How many ranked records the window skipped.</param>
/// <param name="Read">How many records the page holds: the window's size, or fewer when fewer are left after the skipped ones (a record gone since the search or past the deadline is still counted here; the degraded list says why it is missing).</param>
public sealed record RecordMatches([property: JsonPropertyName("matched")] int Matched, [property: JsonPropertyName("offset")] int Offset, [property: JsonPropertyName("read")] int Read);
