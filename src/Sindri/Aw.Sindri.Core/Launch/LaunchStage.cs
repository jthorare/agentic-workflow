using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>Where a failed launch stopped (launcher spec §3 timing table): 'launch.failed' records it as 'detail.stage'.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LaunchStage>))]
public enum LaunchStage
{
    /// <summary>Before the host session existed: the catalog, the budget, the approval check or the host start (4 s).</summary>
    [JsonStringEnumMemberName("host")]
    Host,

    /// <summary>E-1's 10 s outer limit fired because the launch itself hung.</summary>
    [JsonStringEnumMemberName("backstop")]
    Backstop,
}
