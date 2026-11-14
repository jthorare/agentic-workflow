using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Truth;

/// <summary>What an open PR of a project waits on, from Sindri's own PR records (project truth, issue 3718). Conflict and changes-requested come when Context stores them.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectPrState>))]
public enum ProjectPrState
{
    /// <summary>A draft.</summary>
    [JsonStringEnumMemberName("draft")]
    Draft,

    /// <summary>Open, nothing blocks it that Sindri can see.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>The newest summary of its head says CI failed.</summary>
    [JsonStringEnumMemberName("ciRed")]
    CiRed,

    /// <summary>Open, and no CI summary of its current head exists yet (never counted as green).</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,

    /// <summary>A review is requested, or commits landed after a review.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,
}
