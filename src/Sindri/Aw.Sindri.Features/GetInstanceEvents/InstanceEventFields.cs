using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.GetInstanceEvents;

/// <summary>
/// The fields 'workflow events' reads from a workflow event's payload (engine spec §5.6): the states it moved between,
/// the trigger, the outcome and the run. Every field is optional, so a payload with more (or other) fields still reads.
/// </summary>
/// <param name="From">The state before.</param>
/// <param name="To">The state after.</param>
/// <param name="Trigger">The trigger kind ('start', 'step.completed', …).</param>
/// <param name="Outcome">The trigger's outcome.</param>
/// <param name="Run">The run token.</param>
/// <param name="Via">'block' when the engine ran the Step on the block runtime.</param>
/// <param name="Decision">The Judge decision id of a block Step's result.</param>
public sealed record InstanceEventFields(string? From = null, string? To = null, string? Trigger = null, string? Outcome = null, string? Run = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Decision = null)
{
    /// <summary>No fields: a payload that doesn't read as a workflow transition.</summary>
    public static InstanceEventFields None { get; } = new();
}
