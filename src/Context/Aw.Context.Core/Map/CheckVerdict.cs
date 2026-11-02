using System.Text.Json.Serialization;

namespace Aw.Context.Core.Map;

/// <summary>How one structure measure came out.</summary>
[JsonConverter(typeof(StrictEnumConverter<CheckVerdict>))]
public enum CheckVerdict
{
    /// <summary>At or under the degraded limit, or not measured.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass,

    /// <summary>Over the degraded limit: the draft is saved with a note.</summary>
    [JsonStringEnumMemberName("degraded")]
    Degraded,

    /// <summary>Over the refuse limit: no draft is saved.</summary>
    [JsonStringEnumMemberName("refuse")]
    Refuse,
}
