using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Events;

/// <summary>A suspension as a delta shows it: why, never the question.</summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">The park reason.</param>
/// <param name="ParkKind">The park's kind.</param>
/// <param name="Escalation">The escalation's kind.</param>
/// <param name="Mirrors">The child it mirrors.</param>
/// <param name="MirroredParkKind">For a mirror of a park: what the child's own park is about; omitted when none or not carried.</param>
/// <param name="MirroredEscalation">For a mirror of an escalation: the child's own escalation kind; omitted when none or not carried.</param>
/// <param name="MirrorsAddress">The mirrored child's address, which the interpreter knows and the id doesn't give back; omitted when none (engine P7: the needs-you row names the child).</param>
public sealed record SuspensionSummary(
    SuspensionKind Kind,
    ParkReason? Reason,
    ParkKind? ParkKind,
    EscalationKind? Escalation,
    string? Mirrors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MirrorsAddress = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ParkKind? MirroredParkKind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EscalationKind? MirroredEscalation = null)
{
    /// <summary>The sandbox refusal behind a no-runtime or launch park (sandbox spec §6); omitted for every other suspension.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SandboxPark? Sandbox { get; init; }
}
