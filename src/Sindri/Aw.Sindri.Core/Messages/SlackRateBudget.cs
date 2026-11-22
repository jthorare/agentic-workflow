namespace Aw.Sindri.Core.Messages;

/// <summary>
/// The one rate budget per Slack app (messages spec §5.3): a 429 holds that method only, until Slack's Retry-After has passed, and every other method
/// stays callable. One instance per app id; it is safe to share between threads.
/// </summary>
public sealed class SlackRateBudget
{
    private readonly Dictionary<string, DateTimeOffset> _holds = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Records Slack's 429 for a method: it waits until <paramref name="now"/> plus <paramref name="retryAfter"/>. A later hold already in place stays.</summary>
    /// <param name="method">The Slack method ('conversations.history').</param>
    /// <param name="now">The clock's time.</param>
    /// <param name="retryAfter">Slack's Retry-After.</param>
    public void Hold(string method, DateTimeOffset now, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(method);
        var until = now + (retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
        lock (_gate)
        {
            if (!_holds.TryGetValue(method, out var held) || held < until)
            {
                _holds[method] = until;
            }
        }
    }

    /// <summary>Whether a method may be called now.</summary>
    /// <param name="method">The Slack method.</param>
    /// <param name="now">The clock's time.</param>
    /// <returns>True when the method has no hold, or its hold has passed.</returns>
    public bool MayCall(string method, DateTimeOffset now) => Wait(method, now) == TimeSpan.Zero;

    /// <summary>How long a method still waits.</summary>
    /// <param name="method">The Slack method.</param>
    /// <param name="now">The clock's time.</param>
    /// <returns>Zero when the method may be called; else the time left on its hold.</returns>
    public TimeSpan Wait(string method, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(method);
        lock (_gate)
        {
            if (!_holds.TryGetValue(method, out var until))
            {
                return TimeSpan.Zero;
            }

            if (until > now)
            {
                return until - now;
            }

            _holds.Remove(method);
            return TimeSpan.Zero;
        }
    }
}
