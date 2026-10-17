using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launches;

/// <summary>Where a launch request is (UI spec E-1's 'LaunchView.state').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LaunchState>))]
public enum LaunchState
{
    /// <summary>Asked for; the launcher hasn't recorded an outcome.</summary>
    [JsonStringEnumMemberName("requested")]
    Requested,

    /// <summary>The session exists ('launch.started').</summary>
    [JsonStringEnumMemberName("started")]
    Started,

    /// <summary>No session started ('launch.failed').</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
