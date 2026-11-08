namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// The rate-limit policy: Retry-After is honored, and each further failure in a row doubles the wait (429 and 5xx).
/// Without a Retry-After the first wait is <see cref="BaseDelay"/>. Our own doubling is capped at <see cref="MaxDelay"/>; a Retry-After longer than that is honored as given.
/// </summary>
public static class PollBackoff
{
    // 2^20 x 30 s is far over the cap, and a shift past 62 would overflow the ticks.
    private const int MaxDoublings = 20;

    /// <summary>Gets the first wait when the provider names none: 30 seconds.</summary>
    public static TimeSpan BaseDelay { get; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the longest wait: one hour.</summary>
    public static TimeSpan MaxDelay { get; } = TimeSpan.FromHours(1);

    /// <summary>Decides when to poll again.</summary>
    /// <param name="previousFailures">Failures in a row before this one (0 after a good poll).</param>
    /// <param name="retryAfter">The provider's Retry-After, or null.</param>
    /// <param name="now">The time of the failed poll.</param>
    /// <returns>The new failure count and the instant before which no poll runs.</returns>
    public static BackoffDecision Next(int previousFailures, TimeSpan? retryAfter, DateTimeOffset now)
    {
        var failures = Math.Max(previousFailures, 0);
        var named = retryAfter is { } wait && wait > TimeSpan.Zero ? wait : (TimeSpan?)null;
        var first = Math.Min((named ?? BaseDelay).Ticks, MaxDelay.Ticks);
        var doubled = failures >= MaxDoublings ? MaxDelay : TimeSpan.FromTicks(Math.Min(first * (1L << failures), MaxDelay.Ticks));
        return new BackoffDecision(failures + 1, now + (named is { } given && given > doubled ? given : doubled));
    }
}
