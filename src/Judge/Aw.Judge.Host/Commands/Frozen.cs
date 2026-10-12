using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Health;

namespace Aw.Judge.Host.Commands;

/// <summary>The frozen surface's text for domain enums.</summary>
internal static class Frozen
{
    internal static string Text(HealthStatus status) => status switch
    {
        HealthStatus.Ok => "ok",
        HealthStatus.Degraded => "degraded",
        HealthStatus.Down => "down",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Not a health status."),
    };

    internal static string Text(DecisionOutcome outcome) => outcome switch
    {
        DecisionOutcome.Decided => "decided",
        DecisionOutcome.Escalated => "escalated",
        DecisionOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Not a decision outcome."),
    };
}
