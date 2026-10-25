using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What a check run reports: pass or gaps (draft) and complete or incomplete (evidence), or regather when every failing rule is SCP-COV-002.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScopeOutcome>))]
public enum ScopeOutcome
{
    /// <summary>Draft stage, no failing finding.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>Draft stage, the drafter can fix the findings.</summary>
    [JsonStringEnumMemberName("gaps")]
    Gaps = 2,

    /// <summary>Evidence stage, no failing finding and every rule ran.</summary>
    [JsonStringEnumMemberName("complete")]
    Complete = 3,

    /// <summary>Evidence stage, a failing finding or a rule that did not run.</summary>
    [JsonStringEnumMemberName("incomplete")]
    Incomplete = 4,

    /// <summary>Every failing rule is SCP-COV-002: a stale lens, which a re-embed plus another gather can fix. SCP-LENS-001 is not: it is a fault in the lens itself and fails the Step.</summary>
    [JsonStringEnumMemberName("regather")]
    Regather = 5,
}
