using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// A new instance (spec §5.1): a root from 'workflow start', or a child created by its parent's transition (W17).
/// It enters the recipe's initial state as entry 1. With <see cref="TakenOver"/> it starts taken over (UI E-19).
/// </summary>
/// <param name="Key">The key the start is decided under.</param>
/// <param name="Id">The new instance's id.</param>
/// <param name="Address">Its address.</param>
/// <param name="Recipe">The recipe and its pinned children (a child's from its parent's pin).</param>
/// <param name="Item">The item.</param>
/// <param name="Attempt">The attempt.</param>
/// <param name="Parent">The parent, for a child.</param>
/// <param name="Inputs">The input facts.</param>
/// <param name="Actor">Who started it (a child keeps its parent trigger's actor).</param>
/// <param name="TakenOver">Whether it starts taken over, because a launch for the item is still requested.</param>
/// <param name="Tenant">The tenant key, from the workflow input (a child keeps its parent's).</param>
/// <param name="Ring">The ring, from the workflow input (a child keeps its parent's).</param>
public sealed record StartRequest(
    TriggerKey Key,
    WorkflowInstanceId Id,
    InstanceAddress Address,
    PinnedRecipe Recipe,
    ItemKey Item,
    int Attempt,
    WorkflowInstanceId? Parent,
    IReadOnlyDictionary<FactKey, FactValue> Inputs,
    string Actor,
    bool TakenOver,
    string Tenant,
    int Ring);
