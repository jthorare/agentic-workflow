using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>What an open does with the item's agent session (UI spec E-1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LaunchMode>))]
public enum LaunchMode
{
    /// <summary>Start a new session.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Show the live session; start no process.</summary>
    [JsonStringEnumMemberName("attach")]
    Attach,

    /// <summary>Start the agent's resume command for the item's last session.</summary>
    [JsonStringEnumMemberName("resume")]
    Resume,
}
