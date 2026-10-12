using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A gate's verdict (spec §2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GateVerdict>))]
public enum GateVerdict
{
    /// <summary>The transition may proceed.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>The gate refuses the transition.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>The gate can't decide; a top-rung gate escalates.</summary>
    [JsonStringEnumMemberName("uncertain")]
    Uncertain = 3,
}
