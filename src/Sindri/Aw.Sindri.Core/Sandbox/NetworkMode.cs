using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Whether the network allowlist is enforced or in learn mode (owner decision Q13).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NetworkMode>))]
public enum NetworkMode
{
    /// <summary>Only the allowlisted host groups are reachable.</summary>
    [JsonStringEnumMemberName("enforce")]
    Enforce,

    /// <summary>Every host is allowed and recorded, for a bounded first period.</summary>
    [JsonStringEnumMemberName("learn")]
    Learn,
}
