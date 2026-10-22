using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A frame as a ledger row's delta shows it (the engine's FrameSummary): the state a run opened in, and who runs the open run.</summary>
/// <param name="State">The state.</param>
/// <param name="Entry">The entry number.</param>
/// <param name="Run">The open run's token.</param>
/// <param name="Child">The child the frame waits on.</param>
/// <param name="Runtime">Who runs the open run (desk, agent or block); null when no run is open.</param>
/// <param name="Actor">Who runs the open run, on the row that opens it: the agent's launch id, "block", "human:ui", "human:cli" or "human"; omitted when null.</param>
/// <param name="Attempt">The open run's try number for its state's loop; omitted for a state in no loop.</param>
/// <param name="MaxAttempts">The loop cap in force when the run opened; omitted for a state in no loop.</param>
internal sealed record FrameSummary(
    string State,
    int Entry,
    string? Run,
    string? Child,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Runtime = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Actor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Attempt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? MaxAttempts = null);
