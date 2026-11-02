using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.ModelHistory;

/// <summary>How one agent run of a model ended, as the model history counts it (model-pick spec §7.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModelOutcome>))]
public enum ModelOutcome
{
    /// <summary>The step completed.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok = 1,

    /// <summary>The model's report was refused as not a valid result.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused = 2,

    /// <summary>The step failed its own check.</summary>
    [JsonStringEnumMemberName("check_failed")]
    CheckFailed = 3,

    /// <summary>The run died of something that is not the model's doing (the session exited, was lost, went idle or never launched).</summary>
    [JsonStringEnumMemberName("crashed")]
    Crashed = 4,

    /// <summary>The step completed, but the instance went back to redo what this run produced; it replaces the run's 'ok'.</summary>
    [JsonStringEnumMemberName("redraft")]
    Redraft = 5,
}
