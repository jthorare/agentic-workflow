namespace Aw.Judge.Core.Decisions;

/// <summary>One provider failure (the 'failures' table), counted by health.</summary>
/// <param name="Ts">When it failed.</param>
/// <param name="Question">The question being asked.</param>
/// <param name="Provider">The provider that failed.</param>
/// <param name="ReasonCode">Why, e.g. 'timeout' or 'provider-threw'.</param>
public sealed record FailureRecord(DateTimeOffset Ts, string Question, string Provider, string ReasonCode);
