using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>
/// Where a Step runs when its recipe doesn't say otherwise (DT spec §8.2). Descriptor-only until the block runtime (DT-J); no rule reads it today.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlockRuntime>))]
public enum BlockRuntime
{
    /// <summary>The human, reporting with 'sindri workflow complete'.</summary>
    [JsonStringEnumMemberName("desk")]
    Desk = 1,

    /// <summary>The engine's block runtime (a Judge call or a deterministic block).</summary>
    [JsonStringEnumMemberName("block")]
    Block = 2,

    /// <summary>An agent session launched for the run.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent = 3,
}
