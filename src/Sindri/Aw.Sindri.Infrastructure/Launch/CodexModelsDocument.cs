using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The JSON 'codex debug models' prints.</summary>
internal sealed record CodexModelsDocument
{
    [JsonPropertyName("models")]
    public IReadOnlyList<CodexModelRow>? Models { get; init; }
}
