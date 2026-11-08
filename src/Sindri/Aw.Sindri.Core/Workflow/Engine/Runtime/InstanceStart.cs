using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The input of one 'RecipeWorkflow' execution (engine spec rev 8 §5.1): the recipe '(id, version, hash)', the item,
/// the attempt, the input facts, and the tenant and ring keys. The workflow id is <see cref="WorkflowId"/>, so starting
/// the same '(recipe, item, attempt)' again finds the running execution (WorkflowIdConflictPolicy.UseExisting).
/// </summary>
/// <param name="Recipe">The approved recipe the instance is pinned to.</param>
/// <param name="Item">The item, as IItems resolved it.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Inputs">The input facts by key ('fact:spec'), each stored by the API before the start.</param>
/// <param name="Tenant">The tenant key.</param>
/// <param name="Ring">The ring.</param>
public sealed record InstanceStart(
    RecipeVersion Recipe,
    ItemKey Item,
    int Attempt,
    IReadOnlyDictionary<FactKey, InputFact> Inputs,
    string Tenant,
    int Ring)
{
    /// <summary>
    /// The launch ('LCH-…') still 'requested' for the item when the start read the item's launches (UI spec E-19, §6.4):
    /// the instance then starts taken over, with that request as the takeover's causation, so automation never runs
    /// beside a session that's opening. Null when no launch is in flight. Engine P5 contract unit; unit (b) sets and reads it.
    /// </summary>
    public string? OpenLaunch { get; init; }

    /// <summary>
    /// The start time (spec section 3.11): stamped once by the start from its clock, so a generated input and the instance's name read the
    /// same instant (the workflow renders the name from it, never from its own clock).
    /// </summary>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>Who started the instance, in the ledger's actor form ('human:prover', 'session:mcp'); null for a child, an automated start, or a history from before this was recorded.</summary>
    public string? StartedBy { get; init; }

    /// <summary>The address of a child instance, '{parent}/{recipe name}.{entry}' (engine P7 plan D1); null for a root instance, whose address is built from the item, the recipe and the attempt.</summary>
    public InstanceAddress? ChildAddress { get; init; }

    /// <summary>The parent of a child instance (engine P7 plan D1); null for a root instance.</summary>
    public WorkflowInstanceId? ParentId { get; init; }

    /// <summary>The instance this one reruns (a 'RerunInstance'); null for any other start. It is workflow input, so it is in the history and survives a restart.</summary>
    public InstanceAddress? Again { get; init; }

    /// <summary>The workflow id: the instance's address, '{item}/{recipe name}/{attempt}', or a child's own address.</summary>
    /// <returns>The address.</returns>
    public InstanceAddress WorkflowId() => ChildAddress ?? InstanceAddress.Root(Item, Recipe.Id, Attempt);
}
