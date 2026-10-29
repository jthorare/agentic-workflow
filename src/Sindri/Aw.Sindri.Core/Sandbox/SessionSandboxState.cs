using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>Whether a sandboxed session's details can be read.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SessionSandboxState>))]
public enum SessionSandboxState
{
    /// <summary>The plan file reads.</summary>
    [JsonStringEnumMemberName("on")]
    On,

    /// <summary>The launch was sandboxed but its plan file does not read: "details unavailable".</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}
