using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>POST /v1/systemone's body for several questions at once: the state, the model, and the questions by key.</summary>
/// <param name="State">What the questions are about.</param>
/// <param name="Model">The Jev model.</param>
/// <param name="Questions">The questions by key.</param>
internal sealed record JevBatchRequest(JsonNode? State, string Model, IReadOnlyDictionary<string, JevBatchQuestion> Questions)
{
    /// <summary>The images by the reference the state names them with; left out of the body when the batch has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, JevAsset>? Assets { get; init; }
}
