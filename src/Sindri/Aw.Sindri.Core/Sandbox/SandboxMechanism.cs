using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>The mechanism that confined a session (sandbox spec SB12).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SandboxMechanism>))]
public enum SandboxMechanism
{
    /// <summary>bubblewrap with a systemd cgroup scope (Linux).</summary>
    [JsonStringEnumMemberName("bubblewrap")]
    Bubblewrap,

    /// <summary>Seatbelt (macOS, SB-9).</summary>
    [JsonStringEnumMemberName("seatbelt")]
    Seatbelt,

    /// <summary>The test-only pass-through; never in the product Host.</summary>
    [JsonStringEnumMemberName("pass-through")]
    PassThrough,
}
