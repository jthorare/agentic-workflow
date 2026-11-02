using System.Text.RegularExpressions;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Tells a harness's usage-limit message from any other failure text. Each pattern is anchored at the start of the message
/// (the real strings begin "You've hit your" or "Claude usage limit reached"; only blanks and marks like "⎿" may come before), because an
/// error can echo the prompt, which carries repo paths and text. Callers hand it only text from a channel the CLI marks as an
/// error, never a model's answer. No reset time is parsed.
/// </summary>
internal static partial class UsageLimitMatcher
{
    /// <summary>The pattern for "You've hit your usage limit. Upgrade to Pro (…) or try again at 9:52 PM." (Codex).</summary>
    public const string HitYourUsageLimit = "hit-your-usage-limit";

    /// <summary>The pattern for "You've hit your session limit · resets 2:50am (…)" and its weekly twin (Claude).</summary>
    public const string HitYourLimit = "hit-your-limit";

    /// <summary>The pattern for "Claude usage limit reached. …".</summary>
    public const string UsageLimitReached = "usage-limit-reached";

    /// <summary>The id of the pattern a message matches.</summary>
    /// <param name="message">The error text.</param>
    /// <returns>The pattern's id, or null when the message is not a usage limit.</returns>
    public static string? Match(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return null;
        }

        if (HitYourUsageLimitPattern.IsMatch(message))
        {
            return HitYourUsageLimit;
        }

        if (HitYourLimitPattern.IsMatch(message))
        {
            return HitYourLimit;
        }

        return UsageLimitReachedPattern.IsMatch(message) ? UsageLimitReached : null;
    }

    [GeneratedRegex(@"\A[^\p{L}\p{N}]*you['’]ve\s+hit\s+your\s+usage\s+limit\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HitYourUsageLimitPattern { get; }

    [GeneratedRegex(@"\A[^\p{L}\p{N}]*you['’]ve\s+hit\s+your\s+(?:[A-Za-z0-9._-]+\s+){0,3}limit\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex HitYourLimitPattern { get; }

    [GeneratedRegex(@"\A[^\p{L}\p{N}]*(?:claude\s+)?usage\s+limit\s+reached\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex UsageLimitReachedPattern { get; }
}
