using System.Globalization;

namespace Aw.Sindri.Core.Messages;

/// <summary>The one token bucket per Slack method and app (messages spec §5.3): a 429 pauses that method only. M1d builds it.</summary>
public sealed class SlackRateBudget
{
    private readonly Dictionary<string, DateTimeOffset> _holds = new(StringComparer.Ordinal);

    /// <summary>Records Slack's 429 for a method: it waits until <paramref name="now"/> plus <paramref name="retryAfter"/>.</summary>
    /// <param name="method">The Slack method ('conversations.history').</param>
    /// <param name="now">The clock's time.</param>
    /// <param name="retryAfter">Slack's Retry-After.</param>
    public void Hold(string method, DateTimeOffset now, TimeSpan retryAfter)
    {
        ArgumentNullException.ThrowIfNull(method);
        _ = (now, retryAfter);
        throw MessageStubs.NotBuilt(string.Create(CultureInfo.InvariantCulture, $"SlackRateBudget.Hold ({_holds.Count} held)"), "M1d");
    }

    /// <summary>Whether a method may be called now.</summary>
    /// <param name="method">The Slack method.</param>
    /// <param name="now">The clock's time.</param>
    /// <returns>True when the method has a token and no hold.</returns>
    public bool MayCall(string method, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(method);
        _ = now;
        throw MessageStubs.NotBuilt(string.Create(CultureInfo.InvariantCulture, $"SlackRateBudget.MayCall ({_holds.Count} held)"), "M1d");
    }
}
