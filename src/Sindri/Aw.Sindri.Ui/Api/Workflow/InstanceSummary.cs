namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One row of the instance list.</summary>
/// <param name="Address">Address, as served.</param>
/// <param name="Id">Id, as served.</param>
/// <param name="ItemId">ItemId, as served.</param>
/// <param name="Recipe">Recipe, as served.</param>
/// <param name="State">State, as served.</param>
/// <param name="Chip">Chip, as served.</param>
/// <param name="Since">Since, as served.</param>
/// <param name="EngineOwned">EngineOwned, as served.</param>
/// <param name="Subject">Subject, as served.</param>
/// <param name="Inputs">Inputs, as served.</param>
/// <param name="States">States, as served.</param>
/// <param name="Now">Now, as served.</param>
/// <param name="Answer">Answer, as served.</param>
/// <param name="Actions">Actions, as served.</param>
/// <param name="StartedBy">StartedBy, as served; null when nobody is recorded.</param>
/// <param name="StartedAt">StartedAt, as served; null for an older instance.</param>
/// <param name="LineStage">LineStage, as served; null when the item has none.</param>
/// <param name="RootAddress">RootAddress, as served.</param>
/// <param name="WaitingOn">Who the instance waits on: 'you', 'agent' or 'engine'; null once it ended (also null from a server before this field).</param>
/// <param name="Again">The address of the instance this one reruns; read as optional.</param>
internal sealed record InstanceSummary(
    string Address,
    string Id,
    string? ItemId,
    InstanceRecipe Recipe,
    string State,
    StateChip Chip,
    DateTimeOffset Since,
    EngineOwnedBadge? EngineOwned,
    SubjectRef Subject,
    IReadOnlyList<InputPresence> Inputs,
    IReadOnlyList<StateCell> States,
    string Now,
    StatusAnswer? Answer,
    IReadOnlyList<string> Actions,
    string? StartedBy = null,
    DateTimeOffset? StartedAt = null,
    string? LineStage = null,
    string? RootAddress = null,
    string? WaitingOn = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Again = null);
