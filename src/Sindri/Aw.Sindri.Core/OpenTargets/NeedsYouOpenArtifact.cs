using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.OpenTargets;

/// <summary>An artifact a row opens.</summary>
/// <param name="Kind">The kind of artifact.</param>
/// <param name="Fact">The fact it evidences.</param>
/// <param name="Path">Its path; null (omitted) when it has none.</param>
/// <param name="Url">Its address; null (omitted) when it has none.</param>
public sealed record NeedsYouOpenArtifact(
    string Kind,
    string Fact,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null);
