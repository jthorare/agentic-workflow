using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Which limit the session watch saw kill an agent Step's session; the adapters map it from the ledger's limit kind, so Runtime stays below Events.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentLimitKind>))]
public enum AgentLimitKind
{
    /// <summary>The memory ceiling; the limit and peak are bytes.</summary>
    [JsonStringEnumMemberName("memory")]
    Memory,

    /// <summary>The wall clock; the limit and peak are seconds.</summary>
    [JsonStringEnumMemberName("wall")]
    Wall,
}
