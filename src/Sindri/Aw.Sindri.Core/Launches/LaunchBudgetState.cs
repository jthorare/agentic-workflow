using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launches;

/// <summary>Where an agent stands against its daily budget (screens spec E-28).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LaunchBudgetState>))]
public enum LaunchBudgetState
{
    /// <summary>The profile sets no budget for the agent.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Under the soft limit.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>At or over the soft limit (default 80 %) and under the budget: a warning.</summary>
    [JsonStringEnumMemberName("soft")]
    Soft,

    /// <summary>At or over the budget: opens are refused (SND-LAUNCH-010).</summary>
    [JsonStringEnumMemberName("hard")]
    Hard,

    /// <summary>A budget is set but the agent has no usage reader, so the spend can't be known: refused (SND-LAUNCH-010).</summary>
    [JsonStringEnumMemberName("no-usage-reader")]
    NoUsageReader,
}
