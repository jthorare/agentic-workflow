using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Transcripts.Core.Prompts;

/// <summary>
/// Cursor's user-line text: the typed prompt sits in '&lt;user_query&gt;' blocks, and a '&lt;timestamp&gt;' block such as
/// 'Thursday, Sep 3, 2026, 4:45 PM (UTC-7)' is the only clock Cursor writes (minute precision).
/// </summary>
public static partial class CursorPrompt
{
    /// <summary>The typed part of a user line: the '&lt;user_query&gt;' blocks joined by newlines, or, with none, the text after its leading tag blocks.</summary>
    /// <param name="text">The text of the line's text blocks.</param>
    /// <returns>The prompt (may be empty).</returns>
    public static string Body(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var queries = UserQuery.Matches(text).Select(m => m.Groups["query"].Value.Trim()).Where(q => q.Length > 0).ToList();
        return queries.Count > 0 ? string.Join('\n', queries) : PromptText.StripLeadingTagBlocks(text);
    }

    /// <summary>The time in the line's '&lt;timestamp&gt;' block.</summary>
    /// <param name="text">The text of the line's text blocks.</param>
    /// <returns>The time, or null when there is no block or it doesn't parse.</returns>
    public static DateTimeOffset? Timestamp(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var block = TimestampBlock.Match(text);
        if (!block.Success)
        {
            return null;
        }

        var m = CursorTime.Match(block.Groups["time"].Value.Trim());
        if (!m.Success || !DateTime.TryParseExact($"{Month(m.Groups["month"].Value)} {m.Groups["day"].Value} {m.Groups["year"].Value} {m.Groups["time"].Value} {m.Groups["half"].Value.ToUpperInvariant()}",
                "MMM d yyyy h:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        var offset = m.Groups["sign"].Success
            ? (m.Groups["sign"].Value == "-" ? -1 : 1) * new TimeSpan(int.Parse(m.Groups["hours"].Value, CultureInfo.InvariantCulture), m.Groups["minutes"].Success ? int.Parse(m.Groups["minutes"].Value, CultureInfo.InvariantCulture) : 0, 0)
            : TimeSpan.Zero;
        return offset.Duration() <= TimeSpan.FromHours(14) ? new DateTimeOffset(local, offset).ToUniversalTime() : null;
    }

    // "sep" or "SEP" as "Sep", the form the MMM format reads.
    private static string Month(string name) => string.Concat(name[..1].ToUpperInvariant(), name[1..].ToLowerInvariant());

    [GeneratedRegex("<user_query>(?<query>.*?)</user_query>", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex UserQuery { get; }

    [GeneratedRegex("<timestamp>(?<time>.*?)</timestamp>", RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TimestampBlock { get; }

    [GeneratedRegex("^(?:[A-Za-z]+,\\s*)?(?<month>[A-Za-z]{3})[A-Za-z]*\\.?\\s+(?<day>\\d{1,2}),\\s*(?<year>\\d{4}),\\s*(?<time>\\d{1,2}:\\d{2})\\s*(?<half>AM|PM|am|pm)\\s*\\(UTC(?:(?<sign>[+-])(?<hours>\\d{1,2})(?::?(?<minutes>\\d{2}))?)?\\)\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CursorTime { get; }
}
