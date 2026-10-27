using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>Where a model in an agent's list came from (model-pick spec §3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ModelSource>))]
public enum ModelSource
{
    /// <summary>The agent's own list, read at the launch probe (Codex: 'codex debug models').</summary>
    [JsonStringEnumMemberName("discovered")]
    Discovered,

    /// <summary>The launcher catalog entry's aliases (Claude Code has no list command).</summary>
    [JsonStringEnumMemberName("catalog")]
    Catalog,

    /// <summary>The profile's 'workflow.agent.models' list for the agent.</summary>
    [JsonStringEnumMemberName("profile")]
    Profile,
}
