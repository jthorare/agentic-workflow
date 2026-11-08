using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Events;

/// <summary>
/// The payload of a workflow ledger event (spec §5.6): one per instance a decision changes, written by the
/// interpreter's RecordEvent activity. Every event carries the trigger, any gate, evidence, grant or override data,
/// the effects it runs, and the post-state delta. RecordEvent scrubs the whole serialized payload before it is appended (E20).
/// </summary>
/// <param name="Type">The event type.</param>
/// <param name="Instance">The instance it changed.</param>
/// <param name="Trigger">The trigger.</param>
/// <param name="Started">The whole new instance, for workflow.started.</param>
/// <param name="Delta">The post-state delta.</param>
/// <param name="Gates">The gates' answers, in order.</param>
/// <param name="Evidence">The evidence checks.</param>
/// <param name="Grant">A grant the trigger made.</param>
/// <param name="Override">An override the trigger made.</param>
/// <param name="Refusal">What followed a refusal, for workflow.refused.</param>
/// <param name="Effects">The identities of the effects the decision runs for this instance (§5.7).</param>
public sealed record WorkflowEvent(
    WorkflowEventType Type,
    WorkflowInstanceId Instance,
    TriggerRecord Trigger,
    WorkflowInstance? Started,
    InstanceDelta Delta,
    IReadOnlyList<GateRecord> Gates,
    IReadOnlyList<EvidenceRecord> Evidence,
    GrantRecord? Grant,
    OverrideRecord? Override,
    RefusalRecord? Refusal,
    IReadOnlyList<EffectIdentity> Effects);
