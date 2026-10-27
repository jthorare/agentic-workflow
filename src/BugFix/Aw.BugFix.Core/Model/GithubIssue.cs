using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.BugFix.Core.Model;

/// <summary>Which of main's commits belong to a GitHub issue ticket: the ones whose message says "Part of #N" or "Fixes #N".</summary>
public static partial class GithubIssue
{
    /// <summary>The issue number of a GitHub ticket ("#12", "12", "owner/repo#12", an issue URL); null for any other ticket.</summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The number, or null.</returns>
    public static int? Number(Ticket ticket) =>
        ticket.Source == TicketSource.Github && TrailingNumber.Match(ticket.Reference ?? string.Empty) is { Success: true } m
            && int.TryParse(m.Groups["n"].ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    /// <summary>Whether a commit message says it is part of, or fixes, issue <paramref name="number"/> (not #12 for #123).</summary>
    /// <param name="message">The commit message.</param>
    /// <param name="number">The issue number.</param>
    /// <returns>True when it names the issue.</returns>
    public static bool Names(string message, int number) =>
        Reference.Matches(message).Any(m => string.Equals(m.Groups["n"].Value, number.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));

    [GeneratedRegex(@"(?<n>\d+)\s*/?\s*$", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingNumber { get; }

    [GeneratedRegex(@"\b(?:part\s+of|fixes)\s+(?:[\w.-]+/[\w.-]+)?#(?<n>\d+)(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }
}
