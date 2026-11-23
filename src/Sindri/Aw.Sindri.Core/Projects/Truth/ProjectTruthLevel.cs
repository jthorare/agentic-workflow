using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>The level of the health Sindri derives from a project's PRs. Order: offTrack (half or more PRs red), atRisk (any red), unknown (none red, some not checked), onTrack (every PR checked, none red).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectTruthLevel>))]
public enum ProjectTruthLevel
{
    /// <summary>Every open PR is checked and none is red.</summary>
    [JsonStringEnumMemberName("onTrack")]
    OnTrack,

    /// <summary>At least one PR is red.</summary>
    [JsonStringEnumMemberName("atRisk")]
    AtRisk,

    /// <summary>Half the PRs or more are red.</summary>
    [JsonStringEnumMemberName("offTrack")]
    OffTrack,

    /// <summary>None is red, but at least one PR has no CI summary of its current head.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
