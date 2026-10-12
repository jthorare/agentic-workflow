using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>How a gate decides (spec §2, W4): from its descriptor, never from the recipe. Rung-2 and rung-3 gates need a threshold.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Rung>))]
public enum Rung
{
    /// <summary>A deterministic check.</summary>
    [JsonStringEnumMemberName("deterministic")]
    Deterministic = 1,

    /// <summary>A judge question with a score.</summary>
    [JsonStringEnumMemberName("judgeQuestion")]
    JudgeQuestion = 2,

    /// <summary>A bounded model call with a score.</summary>
    [JsonStringEnumMemberName("boundedModel")]
    BoundedModel = 3,
}
