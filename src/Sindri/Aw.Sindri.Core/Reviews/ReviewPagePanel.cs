using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One panel: its id, state, the words of an empty or error state, and its rows.</summary>
/// <param name="Key">requiredChecks, docMap, testPlan, ci, evidence, risk or conversation.</param>
/// <param name="State">ready, empty or error.</param>
/// <param name="Text">The empty or error words; null when ready.</param>
/// <param name="Data">The rows; the type matches the id.</param>
[JsonConverter(typeof(ReviewPagePanelConverter))]
public sealed record ReviewPagePanel(
    [property: JsonPropertyName("id")] string Key,
    string State,
    string? Text,
    ReviewPanelData Data);
