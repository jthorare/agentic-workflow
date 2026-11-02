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
/// <param name="Via">'block' when the engine ran the Step on the block runtime, 'agent' when an agent session ran it.</param>
/// <param name="Decision">The Judge decision id of a block Step's result.</param>
public sealed record InstanceEventFields(string? From = null, string? To = null, string? Trigger = null, string? Outcome = null, string? Run = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Via = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Decision = null)
{
    /// <summary>The tokens an agent run spent; read for 'via agent · tokens N'. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Tokens { get; init; }

    /// <summary>The launcher agent an agent run ran on (#1269). Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Agent { get; init; }

    /// <summary>The model an agent run asked for (#1269). Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Model { get; init; }

    /// <summary>Why a Step failed ('launch', 'lost', 'exit' or 'limit', or a free text); read from the detail of a 'step.failed' row only. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>How a sandbox limit ended the run, on a 'step.failed' row whose reason is 'limit' and whose session end is on the ledger. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InstanceLimitEnding? Limit { get; init; }

    /// <summary>No fields: a payload that doesn't read as a workflow transition.</summary>
    public static InstanceEventFields None { get; } = new();
}
