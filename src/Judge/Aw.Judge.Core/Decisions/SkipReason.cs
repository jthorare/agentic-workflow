using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Decisions;

/// <summary>Why a provider in the chain didn't decide; its JSON text is 'unavailable', 'failed', 'timeout' or 'below_threshold'.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SkipReason>))]
public enum SkipReason
{
    /// <summary>The provider isn't installed or has no key.</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable = 1,

    /// <summary>The provider returned an error.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed = 2,

    /// <summary>The provider ran past the question's time budget.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout = 3,

    /// <summary>The provider answered below the question's threshold.</summary>
    [JsonStringEnumMemberName("below_threshold")]
    BelowThreshold = 4,
}
