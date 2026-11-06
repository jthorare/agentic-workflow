using System.Text.Json.Serialization;

namespace Aw.Context.Core.Library;

/// <summary>A design doc's front matter as read. Values are kept as written, so a bad one (SND-LIB-010) can still be shown; keys the convention doesn't name are dropped.</summary>
/// <param name="AwDoc">The convention version; always 1 (anything else is a near miss with no front matter).</param>
/// <param name="Slug">The doc id (named Slug because a raw string called Id is a typed-id violation, AW1001; it is the "id" key in JSON).</param>
/// <param name="Kind">The doc kind.</param>
/// <param name="Title">The title.</param>
/// <param name="Status">The status.</param>
/// <param name="Project">A Sindri project id or <c language="text">linear:project/&lt;id&gt;</c>.</param>
/// <param name="Repos">The <c language="text">owner/name</c> repos.</param>
/// <param name="Issues">GitHub issue numbers (as digits) and Linear keys.</param>
/// <param name="Supersedes">The doc this one replaces.</param>
/// <param name="Parent">The doc this one belongs to.</param>
/// <param name="Generated">The tool that wrote a generated file.</param>
/// <param name="SourceSha256">The sha256 of a generated file's source.</param>
public sealed record DesignDocFrontMatter(
    [property: JsonPropertyName("aw-doc")] int AwDoc,
    [property: JsonPropertyName("id")] string? Slug,
    string? Kind,
    string? Title,
    string? Status,
    string? Project,
    IReadOnlyList<string>? Repos,
    IReadOnlyList<string>? Issues,
    string? Supersedes,
    string? Parent,
    string? Generated,
    [property: JsonPropertyName("source-sha256")] string? SourceSha256)
{
    /// <summary>The line (from 1) each key is on, for errors decided later; not part of the parsed shape.</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, int> KeyLines { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);
}
