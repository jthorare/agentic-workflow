using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Whether the agent CLI's own sandbox is on inside Sindri's (sandbox spec §4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InnerSandbox>))]
public enum InnerSandbox
{
    /// <summary>Sindri tries the CLI's sandbox once per box and keeps it only if it starts (the profile's default).</summary>
    [JsonStringEnumMemberName("auto")]
    Auto,

    /// <summary>On.</summary>
    [JsonStringEnumMemberName("on")]
    On,

    /// <summary>Off.</summary>
    [JsonStringEnumMemberName("off")]
    Off,
}
