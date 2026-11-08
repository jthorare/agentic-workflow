using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Events;

/// <summary>A frame as a delta shows it.</summary>
/// <param name="State">The state.</param>
/// <param name="Entry">The entry number.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Child">The child the frame waits on.</param>
/// <param name="Runtime">Who runs the open run (desk or agent); null when no run is open. A row from before this field with an open run reads as desk (the only runtime before the agent runtime, P8).</param>
public sealed record FrameSummary(
    string State,
    int Entry,
    string? Run,
    string? Child,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StepRuntime? Runtime = null)
{
    /// <summary>Who runs the open run, on the row that opens it: the agent's launch id, 'block' for the engine's block runtime, or the human at the desk ('human:ui', 'human:cli' when the opening trigger came from one, else 'human'). Not written when null (no run opened, or a row from before this field).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Actor { get; init; }

    /// <summary>The open run's try number for its state's loop (the loop's round after the row); null for a state in no loop. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Attempt { get; init; }

    /// <summary>The loop cap in force when the run opened (the loop's allowance, a granted round included); null for a state in no loop. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxAttempts { get; init; }
}
