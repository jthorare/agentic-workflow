using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Whether the agent CLI's own sandbox ran inside Sindri's, resolved for one launch (a <see cref="SandboxSummary"/> describes the plan, so it is never 'auto').</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InnerState>))]
public enum InnerState
{
    /// <summary>The CLI's own sandbox is on.</summary>
    [JsonStringEnumMemberName("on")]
    On,

    /// <summary>The CLI's own sandbox is off.</summary>
    [JsonStringEnumMemberName("off")]
    Off,
}
