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
    /// <returns>The reviewer's standing OK (with its qa line) and the newest ready message with its counts.</returns>
    public static ReviewFacts Collect(IReadOnlyList<InboxMessage> messages, string reviewer, int pr)
    {
        ArgumentNullException.ThrowIfNull(messages);

        // Replay the reviewer's lines in order: an OK sets the standing OK, a HOLD clears it. A later message that names
        // neither keeps whatever stands, so a reviewer's unrelated message never voids an OK.
        var standing = (Ok: (string?)null, Qa: Qa.None);
        foreach (var message in messages.Where(m => string.Equals(m.FromName, reviewer, StringComparison.Ordinal)))
        {
            standing = Replay(message.Body, pr, standing);
        }

        var ready = messages.LastOrDefault(m => m.Class is { Kind: MessageKind.Ready } c && c.Pr == pr);
        return new ReviewFacts
        {
            Reviewer = reviewer,
            OkSha = standing.Ok,
            QaNotYet = standing.Qa.NotYet,
            QaSha = standing.Qa.Sha,
            ReadySha = ready is null ? null : ReadySha(ready.Body),
            Counts = ready is null ? [] : Counts(ready.Body),
        };
    }

    private readonly record struct Qa(bool NotYet, string? Sha)
    {
        public static Qa None => default;
    }

    // One reviewer message, line by line: "OK #pr at sha" (with the qa line right after it) or "HOLD #pr".
    private static (string? Ok, Qa Qa) Replay(string body, int pr, (string? Ok, Qa Qa) standing)
    {
        var lines = body.Split('\n');
        try
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var ok = OkLine.Match(lines[i]);
                if (ok.Success && SamePr(ok, pr))
                {
                    standing = (ok.Groups["sha"].Value, i + 1 < lines.Length ? QaOf(lines[i + 1]) : Qa.None);
                }
                else if (HoldLine.Match(lines[i]) is { Success: true } hold && SamePr(hold, pr))
                {
                    standing = (null, Qa.None);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return standing;
        }

        return standing;
    }

    private static bool SamePr(Match match, int pr) =>
        int.TryParse(match.Groups["pr"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n == pr;

    private static Qa QaOf(string line)
    {
        var qa = QaLine.Match(line);
        return !qa.Success ? Qa.None : qa.Groups["sha"].Success ? new Qa(NotYet: false, qa.Groups["sha"].Value) : new Qa(NotYet: true, Sha: null);
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

    [GeneratedRegex(@"\A[ \t]*OK #(?<pr>[0-9]+) at (?<sha>[0-9a-f]{7,40})(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex OkLine { get; }

    [GeneratedRegex(@"\A[ \t]*HOLD #(?<pr>[0-9]+)(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex HoldLine { get; }

    [GeneratedRegex(@"\A[ \t]*qa (?:not yet|passed (?<sha>[0-9a-f]{7,40}))(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex QaLine { get; }

    [GeneratedRegex(@"\Aready #[0-9]+ (?:`(?<sha>[0-9a-f]{7,40})`|(?<sha>[0-9a-f]{7,40})(?!`))(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex ReadyLine { get; }

    [GeneratedRegex(@"(?<![0-9])(?<p>[0-9]+) passed, (?<s>[0-9]+) skipped, (?<f>[0-9]+) failed\b", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex CountPattern { get; }
}
