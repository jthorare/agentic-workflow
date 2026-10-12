using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>What a parent does when its child parks (spec §3.4). A child's escalated and cancelled always propagate.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnParked>))]
public enum OnParked
{
    /// <summary>The parent mirrors the park.</summary>
    [JsonStringEnumMemberName("propagate")]
    Propagate = 1,

    /// <summary>The parent keeps waiting.</summary>
    [JsonStringEnumMemberName("ignore")]
    Ignore = 2,
}
