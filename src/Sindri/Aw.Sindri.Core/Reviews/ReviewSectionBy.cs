using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>What a section groups by.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReviewSectionBy>))]
public enum ReviewSectionBy
{
    /// <summary>A project whose review settings name the repo.</summary>
    [JsonStringEnumMemberName("project")]
    Project,

    /// <summary>The repo, when no project names it.</summary>
    [JsonStringEnumMemberName("repo")]
    Repo,
}
