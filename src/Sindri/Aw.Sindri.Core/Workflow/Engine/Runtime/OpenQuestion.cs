using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The open question of a park, escalation or takeover (engine spec rev 8 §5.8, §6.4 'next' and 'answer').</summary>
/// <param name="Kind">Park, escalation or takeover.</param>
/// <param name="Reason">A park's reason.</param>
/// <param name="Question">The question or detail the human reads, scrubbed.</param>
/// <param name="Code">A park's typed code (<see cref="ParkCodes.Of"/>); null when it has none.</param>
public sealed record OpenQuestion(SuspensionKind Kind, ParkReason? Reason, string Question,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Code = null)
{
    /// <summary>The sandbox refusal behind a no-runtime or launch park (sandbox spec §6); null (not written) for every other question.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public SandboxPark? Sandbox { get; init; }
}
