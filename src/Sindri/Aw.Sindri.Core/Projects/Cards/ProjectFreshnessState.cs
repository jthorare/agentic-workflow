using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>A source's state on the freshness line.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectFreshnessState>))]
public enum ProjectFreshnessState
{
    /// <summary>Synced and clean.</summary>
    [JsonStringEnumMemberName("ok")]
    Ok,

    /// <summary>Hasn't finished its first read.</summary>
    [JsonStringEnumMemberName("syncing")]
    Syncing,

    /// <summary>Its last run failed, or it can't run (a missing token).</summary>
    [JsonStringEnumMemberName("failing")]
    Failing,
}
