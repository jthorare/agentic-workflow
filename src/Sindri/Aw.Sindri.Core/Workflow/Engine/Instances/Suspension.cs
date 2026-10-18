namespace Aw.Sindri.Core.Workflow.Engine.Instances;

/// <summary>
/// A park, escalation or takeover (spec §3.8, §5.1, §5.8): what the human reads, and what the answer that ends it
/// runs. The engine-owned state status shows ('parked', 'escalated', 'taken-over') is its kind.
/// </summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">A park's reason.</param>
/// <param name="ParkKind">Which §3.8 row a park is, where its reason has several.</param>
/// <param name="Escalation">An escalation's kind.</param>
/// <param name="Question">The question or detail ('{question}', '{reasons}', '{childNext}' …), scrubbed.</param>
/// <param name="Actor">Who caused it: the takeover's caller, or the trigger's actor for a park or escalation.</param>
/// <param name="Mirrors">The child it mirrors (§5.8): answers go to that child, and that child's own changes end it.</param>
/// <param name="Resume">What the answer (or the handback) that ends it runs.</param>
public sealed record Suspension(
    SuspensionKind Kind,
    ParkReason? Reason,
    ParkKind? ParkKind,
    EscalationKind? Escalation,
    string Question,
    string Actor,
    WorkflowInstanceId? Mirrors,
    ResumeAction Resume)
{
    /// <summary>
    /// The state timer a takeover paused and what was left of it (spec §5.5): set by the interpreter on a takeover of an
    /// instance whose timer was running, read by the handback that re-arms it. Null on a park or escalation (a park stops
    /// the timer) and on a takeover over one. Engine P5 contract unit; unit (b) sets and reads it.
    /// </summary>
    public RemainingTimer? Remaining { get; init; }

    /// <summary>
    /// A drift park the re-approval already ended while a takeover held the instance: it waits in the queue only to
    /// re-send its effects once nothing else holds the instance, and is never made the current suspension (spec §3.8).
    /// </summary>
    public bool Approved { get; init; }

    /// <summary>
    /// The sandbox refusal behind a no-runtime or launch park (sandbox spec §6): which code and reason, so the owner text is the golden for that pair instead of 'no runtime in this build'. Null for every other park.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SandboxPark? Sandbox { get; init; }

    /// <summary>The engine-owned state this suspension shows (spec §3.8).</summary>
    public string State => Kind switch
    {
        SuspensionKind.Escalation => EngineStates.Escalated,
        SuspensionKind.Takeover => EngineStates.TakenOver,
        SuspensionKind.Park or _ => EngineStates.Parked,
    };
}
