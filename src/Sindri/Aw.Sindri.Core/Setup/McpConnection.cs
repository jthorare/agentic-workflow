using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Setup;

/// <summary>Where one provider stands for one repo's MCP registration; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<McpConnection>))]
public enum McpConnection
{
    /// <summary>The provider's config has Sindri's entry for the repo.</summary>
    [JsonStringEnumMemberName("connected")]
    Connected,

    /// <summary>A RegisterMcp job for the repo is queued or running.</summary>
    [JsonStringEnumMemberName("connecting")]
    Connecting,

    /// <summary>Nothing registered and no job has failed.</summary>
    [JsonStringEnumMemberName("not-started")]
    NotStarted,

    /// <summary>The newest RegisterMcp job for the repo failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
