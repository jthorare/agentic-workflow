namespace Aw.Judge.Host.Commands;

/// <summary>The newest failure in 'judge health': who failed, why and when.</summary>
/// <param name="Provider">The provider that failed.</param>
/// <param name="ReasonCode">Why, e.g. 'usage-limit' or 'exit-1'.</param>
/// <param name="At">When it failed.</param>
internal sealed record HealthFailureOutput(string Provider, string ReasonCode, DateTimeOffset At);
