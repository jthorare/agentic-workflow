using System.Text.Json.Serialization;

namespace Aw.Context.Core.Review;

/// <summary>How the cleanup agent settled the low-confidence proposals of a draft.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CleanupMode>))]
public enum CleanupMode
{
    /// <summary>The draft had no low-confidence proposal, so no model was asked.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>The agent judged every low-confidence proposal.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent,

    /// <summary>The agent judged some; the rest (over the token cap, or an answer that didn't parse) were removed.</summary>
    [JsonStringEnumMemberName("partial")]
    Partial,

    /// <summary>No model answered, so every low-confidence proposal was removed.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,
}
