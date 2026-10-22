using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A state of a recipe.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Order">Order, as served.</param>
/// <param name="BranchOf">BranchOf, as served.</param>
/// <param name="Why">Why the state matters, in a sentence; read as optional (an older Sindri serves none).</param>
/// <param name="DisplayName">The state's name in words; read as optional (an older Sindri serves none).</param>
internal sealed record RecipeStateView(
    [property: JsonPropertyName("id")] string State,
    int? Order,
    string? BranchOf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null);
