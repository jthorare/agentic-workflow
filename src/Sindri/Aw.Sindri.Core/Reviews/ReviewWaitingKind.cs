using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Why a PR waits on the owner (PR review page spec §6.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReviewWaitingKind>))]
public enum ReviewWaitingKind
{
    /// <summary>The owner's review is requested.</summary>
    [JsonStringEnumMemberName("requested")]
    Requested,

    /// <summary>The owner reviewed it and commits landed since.</summary>
    [JsonStringEnumMemberName("newCommits")]
    NewCommits,
}
