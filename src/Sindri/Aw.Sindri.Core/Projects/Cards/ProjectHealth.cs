using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>A Linear project's health, from its newest project update (projects page spec §3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectHealth>))]
public enum ProjectHealth
{
    /// <summary>On track.</summary>
    [JsonStringEnumMemberName("onTrack")]
    OnTrack,

    /// <summary>At risk.</summary>
    [JsonStringEnumMemberName("atRisk")]
    AtRisk,

    /// <summary>Off track.</summary>
    [JsonStringEnumMemberName("offTrack")]
    OffTrack,
}
