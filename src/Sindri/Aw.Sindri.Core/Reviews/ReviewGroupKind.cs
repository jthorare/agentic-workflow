using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One of the list's two groups.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReviewGroupKind>))]
public enum ReviewGroupKind
{
    /// <summary>Waiting on you.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,

    /// <summary>The owner's own PRs.</summary>
    [JsonStringEnumMemberName("yours")]
    Yours,
}
