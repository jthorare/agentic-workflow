namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// The rate-limit policy: Retry-After is honored, and each further failure in a row doubles the wait (429 and 5xx).
/// Without a Retry-After the first wait is <see cref="BaseDelay"/>. The wait is capped at <see cref="MaxDelay"/>.
/// </summary>
public static class PollBackoff
{
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
        => throw new NotSupportedException("red stub: PR 11 part 1");
}
