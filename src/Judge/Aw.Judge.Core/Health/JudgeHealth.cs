using Aw.Judge.Core.Decisions;

namespace Aw.Judge.Core.Health;

/// <summary>How 'judge health' rates the judge (TS commands.ts runHealth).</summary>
public static class JudgeHealth
{
    /// <summary>How many of the latest model attempts must all have failed for 'down'.</summary>
    public const int DownStreak = 5;

    /// <summary>The window failures are counted over.</summary>
    public static TimeSpan FailureWindow { get; } = TimeSpan.FromHours(24);

    /// <summary>Ok with no failure in the window; down when the last <see cref="DownStreak"/> attempts all failed; degraded otherwise.</summary>
    /// <param name="failures">The failures in the window.</param>
    /// <param name="lastAttempts">The latest model attempts' outcomes (decided or failed), newest first.</param>
    /// <returns>The status.</returns>
    public static HealthStatus Assess(int failures, IReadOnlyList<DecisionOutcome> lastAttempts)
    {
        ArgumentNullException.ThrowIfNull(lastAttempts);
        if (failures == 0)
        {
            return HealthStatus.Ok;
        }

        var allFailed = lastAttempts.Count == DownStreak && lastAttempts.All(static o => o == DecisionOutcome.Failed);
        return allFailed ? HealthStatus.Down : HealthStatus.Degraded;
    }
}
