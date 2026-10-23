using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One row of the list (E-20).</summary>
/// <param name="Address">The CLI address in the engine form (see <see cref="InstanceDetail"/>).</param>
/// <param name="Id">The engine's WFI id, which the id routes take.</param>
/// <param name="ItemId">The item id when the instance is an item's.</param>
/// <param name="Recipe">The recipe stamp.</param>
/// <param name="State">The current state id.</param>
/// <param name="Chip">The state chip: one word and its tone.</param>
/// <param name="Since">When it entered the state.</param>
/// <param name="EngineOwned">The badge that replaces the glyph.</param>
/// <param name="Subject">The subject, from the first input.</param>
/// <param name="Inputs">The inputs, in declaration order.</param>
/// <param name="States">Every state cell in path order.</param>
/// <param name="Now">The engine's next sentence, at most 80 characters.</param>
/// <param name="Answer">The answer the instance waits for, as on its status.</param>
/// <param name="Actions">The action ids allowed now.</param>
/// <param name="StartedBy">Who started the instance, in the actor form of the step runs; null when nobody is recorded.</param>
/// <param name="StartedAt">When it started; null for an older instance.</param>
/// <param name="LineStage">The current state id of the root workflow.line instance the row belongs to (its own state for a root line row); null when its root is not a line instance.</param>
/// <param name="RootAddress">The address of its root instance; its own for a root.</param>
/// <param name="WaitingOn">Who the instance waits on: 'you', 'agent' or 'engine'; null once it ended (see <see cref="Views.WaitingOn"/>).</param>
/// <param name="Again">The address of the instance this one reruns; null (left out of the JSON) for any other.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InstanceSummary(
    InstanceAddress Address,
    WorkflowInstanceId Id,
    ItemKey? ItemId,
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
    IReadOnlyList<ActionId> Actions,
    string? StartedBy = null,
    DateTimeOffset? StartedAt = null,
    string? LineStage = null,
    string? RootAddress = null,
    string? WaitingOn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InstanceAddress? Again = null);
