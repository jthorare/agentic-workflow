using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>What a transition expects of its evidence (spec §3.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EvidenceExpectation>))]
public enum EvidenceExpectation
{
    /// <summary>The evidence passes.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>The evidence fails.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,
}
