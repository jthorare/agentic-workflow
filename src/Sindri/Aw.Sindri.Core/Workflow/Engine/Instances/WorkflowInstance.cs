using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// An instance of a recipe (spec §5.1, rev 8): the interpreter's state, which every decision reads and returns.
/// <see cref="Frame"/> is where the instance stands in its recipe. <see cref="Suspension"/> is the park, escalation or
/// takeover over it, and <see cref="PendingPark"/> is a park that arrived meanwhile, or the park a takeover covers; it
/// takes effect when the suspension ends. Rev 7's two-deep frame stack is these two fields.
/// </summary>
/// <param name="Id">The opaque id.</param>
/// <param name="Address">The CLI address, which is also its workflow id.</param>
/// <param name="Tenant">The tenant key.</param>
/// <param name="Ring">The ring.</param>
/// <param name="Recipe">The recipe and its pinned children.</param>
/// <param name="Item">The item.</param>
/// <param name="Attempt">The attempt, from 1.</param>
/// <param name="Parent">The parent, for a child.</param>
/// <param name="Frame">The recipe frame.</param>
/// <param name="Suspension">The park, escalation or takeover, while there is one.</param>
/// <param name="PendingPark">A park waiting for the suspension to end.</param>
/// <param name="Loops">Each loop's counters, by loop name.</param>
/// <param name="Spent">The token spend.</param>
/// <param name="Facts">The facts, as stored references.</param>
/// <param name="Outcome">The terminal outcome, once the instance ended (a recipe terminal's, or 'cancelled').</param>
/// <param name="LaterParks">The parks queued behind <see cref="PendingPark"/>, in order; null when there are none (the queue is PendingPark then these).</param>
public sealed record WorkflowInstance(
    WorkflowInstanceId Id,
    InstanceAddress Address,
    string Tenant,
    int Ring,
    PinnedRecipe Recipe,
    ItemKey Item,
    int Attempt,
    WorkflowInstanceId? Parent,
    Frame Frame,
    Suspension? Suspension,
    Suspension? PendingPark,
    IReadOnlyDictionary<string, LoopState> Loops,
    TokenSpend Spent,
    IReadOnlyDictionary<FactKey, FactValue> Facts,
    OutcomeName? Outcome,
    IReadOnlyList<Suspension>? LaterParks = null)
{
    /// <summary>The state status shows: the suspension's engine-owned state, else the frame's.</summary>
    [JsonIgnore]
    public string State => Suspension?.State ?? Frame.State;

    /// <summary>The park reason status shows, while the instance is parked.</summary>
    [JsonIgnore]
    public ParkReason? Park => Suspension is { Kind: SuspensionKind.Park } park ? park.Reason : null;

    /// <summary>Whether the instance has ended.</summary>
    [JsonIgnore]
    public bool Ended => Outcome is not null;

    /// <summary>The parks waiting behind the suspension, in order: the pending park, then the queued ones.</summary>
    /// <returns>The pending parks.</returns>
    internal IReadOnlyList<Suspension> PendingParks() => PendingPark is { } pending ? [pending, .. LaterParks ?? []] : [];
}
