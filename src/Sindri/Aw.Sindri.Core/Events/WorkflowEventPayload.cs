using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// The ledger payload of a mirrored workflow event (<see cref="EventTypes.WorkflowStarted"/>,
/// <see cref="EventTypes.WorkflowTransition"/>; engine spec rev 8.1 §5.6). The trigger and the state change, and in <see cref="Detail"/> what
/// else the event records (trigger id and actor, gates, evidence, grant, override, refusal, effects, the post-state delta).
/// </summary>
/// <param name="Instance">The instance address.</param>
/// <param name="From">The state left (empty for a start).</param>
/// <param name="To">The state entered.</param>
/// <param name="Trigger">The trigger's kind.</param>
/// <param name="Outcome">The trigger's outcome.</param>
/// <param name="Run">The run token.</param>
/// <param name="Detail">The rest of the event's data; none for an event the interpreter mirrors without a decision.</param>
public sealed record WorkflowEventPayload(string Instance, string From, string To, string Trigger, string? Outcome, string? Run, WorkflowEventDetail? Detail = null)
{
    /// <summary>
    /// What caused the change when it isn't the trigger itself (engine P5; UI spec E-6, E-19): the launch request
    /// ('LCH-…') an 'open-request' takeover, or a start taken over, answers. Not written when null.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Causation { get; init; }

    /// <summary>'block' when the engine ran the Step on the block runtime (DT spec §8.2). Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Via { get; init; }

    /// <summary>The Judge decision id of a block Step's result ('sindri workflow why'). Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Decision { get; init; }
}
