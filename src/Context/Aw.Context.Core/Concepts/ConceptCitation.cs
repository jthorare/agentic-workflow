using System.Text.Json.Serialization;

namespace Aw.Context.Core.Concepts;

/// <summary>One record a concept rests on.</summary>
/// <param name="Key">The record's canonical key.</param>
/// <param name="Span">The cited span's hash or line range, or null for the whole record; left out when null.</param>
public sealed record ConceptCitation(string Key, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Span = null);
