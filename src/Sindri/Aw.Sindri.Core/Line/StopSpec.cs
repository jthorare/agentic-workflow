using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>The spec as written.</summary>
/// <param name="Text">The markdown.</param>
/// <param name="Assumes">The sections that rest on an option of a decision.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StopSpec(
    string Text,
    IReadOnlyList<StopAssumes> Assumes);
