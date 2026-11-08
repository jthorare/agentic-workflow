using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>Whether a repo is in the approved profile.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RepoState>))]
public enum RepoState
{
    /// <summary>The directory is not in a git repository.</summary>
    [JsonStringEnumMemberName("outside-git")]
    OutsideGit,

    /// <summary>No profile has been approved yet.</summary>
    [JsonStringEnumMemberName("no-approved-profile")]
    NoApprovedProfile,

    /// <summary>The repo is not in the approved profile (it may be in the live one, waiting for approval).</summary>
    [JsonStringEnumMemberName("not-onboarded")]
    NotOnboarded,

    /// <summary>The repo is in the approved profile.</summary>
    [JsonStringEnumMemberName("onboarded")]
    Onboarded,
}
