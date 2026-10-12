using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>Where evidence is collected (spec §3.5).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EvidencePoint>))]
public enum EvidencePoint
{
    /// <summary>At the base commit.</summary>
    [JsonStringEnumMemberName("base")]
    Base = 1,

    /// <summary>At the head commit.</summary>
    [JsonStringEnumMemberName("head")]
    Head = 2,
}
