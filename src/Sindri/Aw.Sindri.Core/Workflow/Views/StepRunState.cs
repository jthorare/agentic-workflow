using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>Where one Step run stands (the instance detail's 'stepRuns').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepRunState>))]
public enum StepRunState
{
    /// <summary>Open: no closing row yet. It stays so while the instance is parked, taken over or escalated, since those leave the frame as it was.</summary>
    [JsonStringEnumMemberName("running")]
    Running = 1,

    /// <summary>Reported an outcome (a 'step.completed' the engine accepted).</summary>
    [JsonStringEnumMemberName("passed")]
    Passed = 2,

    /// <summary>Failed ('step.failed').</summary>
    [JsonStringEnumMemberName("failed")]
    Failed = 3,

    /// <summary>Its report was refused (a gate or evidence check) and the Step ran again or parked.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused = 4,

    /// <summary>Ended by a cancel while open.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 5,

    /// <summary>The instance moved to another frame with no report on the run (an answer, a command or a recipe move); a cancel is <see cref="Cancelled"/>.</summary>
    [JsonStringEnumMemberName("abandoned")]
    Abandoned = 6,
}
