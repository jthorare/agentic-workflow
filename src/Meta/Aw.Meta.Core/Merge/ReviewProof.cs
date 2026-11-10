using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Meta.Core.Messages;

namespace Aw.Meta.Core.Merge;

/// <summary>Reads a reviewer's OK and a lane's ready counts out of the inbox, and decides whether a sha is the head.</summary>
public static partial class ReviewProof
{
    /// <summary>Collects the review facts for <paramref name="pr"/>.</summary>
    /// <param name="messages">Every inbound message, oldest first.</param>
    /// <param name="reviewer">The reviewer lane's session name.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The newest OK from the reviewer and the newest ready message with its counts.</returns>
    public static ReviewFacts Collect(IReadOnlyList<InboxMessage> messages, string reviewer, int pr)
    {
        ArgumentNullException.ThrowIfNull(messages);
        string? ok = null;
        foreach (var message in messages.Where(m => string.Equals(m.FromName, reviewer, StringComparison.Ordinal)))
        {
            ok = OkSha(message.Body, pr) ?? ok;
        }

        var ready = messages.LastOrDefault(m => m.Class is { Kind: MessageKind.Ready } c && c.Pr == pr);
        return new ReviewFacts
        {
            Reviewer = reviewer,
            OkSha = ok,
            ReadySha = ready is null ? null : ReadySha(ready.Body),
            Counts = ready is null ? [] : Counts(ready.Body),
        };
    }

    /// <summary>Finds the sha of the last "OK #pr at sha" line of a message body.</summary>
    /// <param name="body">The message text.</param>
    /// <param name="pr">The pull request number.</param>
    /// <returns>The sha, or null when no line names this pull request.</returns>
    public static string? OkSha(string body, int pr)
    {
        ArgumentNullException.ThrowIfNull(body);
        var wanted = pr.ToString(CultureInfo.InvariantCulture);
        string? found = null;
        try
        {
            foreach (Match match in OkLine.Matches(body))
            {
                if (match.Groups["pr"].Value.TrimStart('0') == wanted)
                {
                    found = match.Groups["sha"].Value;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        return found;
    }

    /// <summary>Finds the sha on the first line of a ready message.</summary>
    /// <param name="body">The message text.</param>
    /// <returns>The sha, or null when the first line has none.</returns>
    public static string? ReadySha(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var match = ReadyLine.Match(body.Trim());
            return match.Success ? match.Groups["sha"].Value : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>Finds every "N passed, M skipped, K failed" line of a message body.</summary>
    /// <param name="body">The message text.</param>
    /// <returns>One entry per line.</returns>
    public static IReadOnlyList<CountLine> Counts(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var lines = new List<CountLine>();
        try
        {
            foreach (Match match in CountPattern.Matches(body))
            {
                if (int.TryParse(match.Groups["p"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var passed)
                    && int.TryParse(match.Groups["s"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var skipped)
                    && int.TryParse(match.Groups["f"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var failed))
                {
                    lines.Add(new CountLine(passed, skipped, failed));
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }

        return lines;
    }

    /// <summary>Whether <paramref name="sha"/> (7 to 40 hex characters) names <paramref name="head"/>.</summary>
    /// <param name="sha">The sha a message names.</param>
    /// <param name="head">The pull request's head.</param>
    /// <returns>True when the head starts with the sha, in any case.</returns>
    public static bool IsHead(string? sha, string head) =>
        sha is { Length: >= 7 } && head is not null && head.StartsWith(sha, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^[ \t]*OK #(?<pr>[0-9]+) at (?<sha>[0-9a-f]{7,40})(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex OkLine { get; }

    [GeneratedRegex(@"\Aready #[0-9]+ (?:`(?<sha>[0-9a-f]{7,40})`|(?<sha>[0-9a-f]{7,40})(?!`))(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex ReadyLine { get; }

    [GeneratedRegex(@"(?<![0-9])(?<p>[0-9]+) passed, (?<s>[0-9]+) skipped, (?<f>[0-9]+) failed\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex CountPattern { get; }
}
