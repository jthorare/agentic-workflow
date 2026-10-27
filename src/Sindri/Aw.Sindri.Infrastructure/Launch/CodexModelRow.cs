using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>One entry of 'codex debug models' (only the fields the reader uses; the rest is skipped).</summary>
internal sealed record CodexModelRow
{
    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("visibility")]
    public string? Visibility { get; init; }

    [JsonPropertyName("priority")]
    public int? Priority { get; init; }
}
