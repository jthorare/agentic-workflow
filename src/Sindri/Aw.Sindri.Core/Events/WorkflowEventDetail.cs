using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// What a mirrored workflow event carries beyond the state change (engine spec rev 8.1 §5.6): the trigger's kind, id and
/// actor, any gate, evidence, grant or override data, the effects it runs, and the post-state delta. A public projection of
/// the decision's <see cref="WorkflowEvent"/>: ids, digests, reasons, counters and actor strings, plus the started row's
/// subject, the one part that carries text taken from facts. It never holds an answer's text or a parked question
/// (RecordEvent scrubs the serialized payload before it is appended).
/// </summary>
/// <param name="TriggerId">The trigger's key, 'wf:{instance}:{trigger}': carries a command's nonce and arguments digest, a report's run and digest, an event's source sequence.</param>
/// <param name="TriggerActor">Who fired the trigger.</param>
/// <param name="SourceSequence">The ledger sequence of the event that satisfied a wait; none for any other trigger.</param>
/// <param name="ArgumentsSha256">The command's arguments digest, for a command, an answer, a cancel or a handback.</param>
/// <param name="ReportSha256">The report's digest, for a Step completion.</param>
/// <param name="Answer">Which option a 'workflow answer' gave.</param>
/// <param name="Amount">The rounds or tokens of a grant answer.</param>
/// <param name="Reason">A step.failed's reason (free text; RecordEvent scrubs the payload it is in).</param>
/// <param name="Gates">The gates' answers, in order.</param>
/// <param name="Evidence">The evidence checks.</param>
/// <param name="Grant">A grant the trigger made.</param>
/// <param name="Override">An override the trigger made.</param>
/// <param name="Refusal">What followed a refusal.</param>
/// <param name="Effects">The identities of the effects the decision runs for this instance, as '{instance}:{entry}:{index}'.</param>
/// <param name="Delta">The post-state delta.</param>
/// <param name="Via">'block' for a Step the engine ran on the block runtime; null otherwise.</param>
/// <param name="Decision">The Judge decision id of a block Step's result; null otherwise.</param>
public sealed record WorkflowEventDetail(
    TriggerKey TriggerId,
    string TriggerActor,
    long? SourceSequence,
    string? ArgumentsSha256,
    string? ReportSha256,
    AnswerVariant? Answer,
    int? Amount,
    string? Reason,
    IReadOnlyList<GateRecord> Gates,
    IReadOnlyList<EvidenceRecord> Evidence,
    GrantRecord? Grant,
    OverrideRecord? Override,
    RefusalRecord? Refusal,
    IReadOnlyList<string> Effects,
    WorkflowEventDelta Delta,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Decision = null)
{
    /// <summary>
    /// The detail of a row mirrored without a decision: a desk timeout's park, or a held event's expiry. It names the trigger
    /// (its key and actor, a wait's source sequence) and the instance's suspension after the row, and nothing else.
    /// </summary>
    /// <param name="trigger">The deadline that fired, or the held event that expired.</param>
    /// <param name="instance">The instance the row is about.</param>
    /// <param name="suspension">The instance's suspension after it; none when it has none.</param>
    /// <returns>The detail the ledger row carries.</returns>
    public static WorkflowEventDetail Of(Trigger trigger, WorkflowInstanceId instance, Suspension? suspension)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        return new WorkflowEventDetail(
            trigger.KeyFor(instance),
            trigger.Actor,
            trigger is WaitSatisfied { Source: EventWaitSource e } ? e.Sequence : null,
            ArgumentsSha256: null,
            ReportSha256: null,
            Answer: null,
            Amount: null,
            Reason: null,
            Gates: [],
            Evidence: [],
            Grant: null,
            Override: null,
            Refusal: null,
            Effects: [],
            new WorkflowEventDelta(Frame: null, WorkflowEventDelta.Summary(suspension), PendingPark: null, Loops: null, TokensSpent: null, TokensGranted: null, Facts: null, Outcome: null));
    }

    /// <summary>The public projection of a decision's event for <paramref name="trigger"/>.</summary>
    /// <param name="recorded">The decision's event for this instance.</param>
    /// <param name="trigger">The trigger the decision decided; none for a start, whose event carries only its key and actor.</param>
    /// <returns>The detail the ledger row carries.</returns>
    public static WorkflowEventDetail Of(WorkflowEvent recorded, Trigger? trigger)
    {
        ArgumentNullException.ThrowIfNull(recorded);
        var stamp = trigger switch
        {
            CommandFired c => c.Stamp,
            AnswerGiven a => a.Stamp,
            CancelRequested { Stamp: { } s } => s,
            HandbackRequested h => h.Stamp,
            _ => null,
        };
        var answer = trigger as AnswerGiven;
        return new WorkflowEventDetail(
            recorded.Trigger.Key,
            recorded.Trigger.Actor,
            trigger switch
            {
                WaitSatisfied { Source: EventWaitSource e } => e.Sequence,
                CancelRequested { Origin: CancelOrigin.Tracker } c => c.Sequence,
                _ => null,
            },
            stamp?.ArgumentsSha256,
            (trigger as StepCompleted)?.ReportSha256,
            answer?.Variant,
            answer?.Amount,
            recorded.Trigger.Reason,
            recorded.Gates,
            recorded.Evidence,
            recorded.Grant,
            recorded.Override,
            recorded.Refusal,
            [.. recorded.Effects.Select(e => e.Text)],
            WorkflowEventDelta.Of(recorded.Delta),
            recorded.Trigger.Via,
            recorded.Trigger.Decision);
    }
}
