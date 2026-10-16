using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>What a query's rows must hold: how many there are, JSON Pointer assertions over them, and a golden file. At least one is given.</summary>
/// <param name="RowCount">The number of rows returned (after the row cap).</param>
/// <param name="Pointers">JSON Pointer assertions over the rows read as a JSON array of objects keyed by column name.</param>
/// <param name="Golden">A file the rows' JSON must equal; a relative path resolves against the script's directory.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DbExpectation(int? RowCount = null, IReadOnlyList<PointerCheck>? Pointers = null, string? Golden = null);
