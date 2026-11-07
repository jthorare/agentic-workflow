using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>A required check (spec §7.3).</summary>
/// <param name="Key">Its id (the YAML key is "id"), unique within the entry.</param>
/// <param name="Title">What the page shows.</param>
/// <param name="When">Globs: the check applies when any changed file matches any of them. Absent: always applies; an empty list is refused.</param>
/// <param name="Need">What must hold.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrReviewCheck(
    [property: JsonPropertyName("id")] string Key,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? When = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PrReviewNeed? Need = null);
