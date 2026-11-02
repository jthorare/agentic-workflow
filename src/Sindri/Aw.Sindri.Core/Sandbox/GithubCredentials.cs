using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>How a session gets GitHub access (sandbox spec §3.4).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GithubCredentials>))]
public enum GithubCredentials
{
    /// <summary>No GitHub access; Sindri pushes host-side (the default).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>The 1 h agent GitHub App token (SB-7).</summary>
    [JsonStringEnumMemberName("agent-app")]
    AgentApp,
}
