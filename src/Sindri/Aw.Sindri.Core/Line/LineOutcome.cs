using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>What a station's check run reports: pass or gaps (draft) and complete or incomplete (evidence).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LineOutcome>))]
public enum LineOutcome
{
    /// <summary>Draft stage, no failing finding.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>Draft stage, the writer can fix the findings.</summary>
    [JsonStringEnumMemberName("gaps")]
    Gaps = 2,

    /// <summary>Evidence stage, no failing finding and every rule ran.</summary>
    [JsonStringEnumMemberName("complete")]
    Complete = 3,

    /// <summary>Evidence stage, a failing finding or a rule that did not run.</summary>
    [JsonStringEnumMemberName("incomplete")]
    Incomplete = 4,
}
