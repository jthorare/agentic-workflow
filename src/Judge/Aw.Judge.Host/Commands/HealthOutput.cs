using System.Text.Json.Serialization;

namespace Aw.Judge.Host.Commands;

/// <summary>What 'judge health' prints: {"status": "ok|degraded|down", "failures24h": N} (hook contract), plus "lastFailure" only when there is one.</summary>
/// <param name="Status">ok, degraded or down.</param>
/// <param name="Failures24h">The provider failures in the last 24 hours.</param>
/// <param name="LastFailure">The newest failure of the last 24 hours; left out of the JSON when null.</param>
internal sealed record HealthOutput(
    string Status,
    int Failures24h,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HealthFailureOutput? LastFailure = null);

/// <summary>The newest failure in 'judge health': who failed, why and when.</summary>
/// <param name="Provider">The provider that failed.</param>
/// <param name="ReasonCode">Why, e.g. 'usage-limit' or 'exit-1'.</param>
/// <param name="At">When it failed.</param>
internal sealed record HealthFailureOutput(string Provider, string ReasonCode, DateTimeOffset At);
