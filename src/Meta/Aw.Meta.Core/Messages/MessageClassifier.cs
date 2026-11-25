using System.Globalization;
using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Messages;

/// <summary>Classifies an inbound message body.</summary>
public static partial class MessageClassifier
{
    private const string OwnerQuestionPrefix = "OWNER QUESTION";

    /// <summary>Classifies <paramref name="body"/>: an owner question, a ready notice, a lane announcement or a request. A leading STATUS: or ACTION: tag is read past (#2651).</summary>
    /// <param name="body">The message text.</param>
    /// <returns>The classification.</returns>
    public static Classification Classify(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var text = NoticeRule.StripTag(body.Trim());
        if (text.StartsWith(OwnerQuestionPrefix, StringComparison.Ordinal))
        {
            return new Classification(MessageKind.OwnerQuestion);
        }

        if (Matches(ReadyPattern, text) is { } ready
            && int.TryParse(ready.Groups["pr"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var pr)
            && Matches(NotReadyPattern, FirstLine(text)) is null)
        {
            return new Classification(MessageKind.Ready, Pr: pr);
        }

        return Matches(AnnouncePattern, text) is { } announce
            ? new Classification(MessageKind.LaneAnnounce, Lane: announce.Groups["lane"].Value.ToLowerInvariant())
            : new Classification(MessageKind.Request);
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return end < 0 ? text : text[..end];
    }

    private static Match? Matches(Regex pattern, string text)
    {
        try
        {
            var match = pattern.Match(text);
            return match.Success ? match : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null; // a timeout never classifies (spec §4c)
        }
    }

    // Only the first line counts, and it must be "ready #N <sha>" (any case, the sha with paired backticks or none); "not ready" or "follows" on that line says it is not one: a status line that merely mentions a later ready never queues the pull request.
    [GeneratedRegex(@"\Aready #(?<pr>[0-9]+) (?:`(?<sha>[0-9a-f]{7,40})`|(?<sha>[0-9a-f]{7,40})(?!`))(?![0-9A-Za-z])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex ReadyPattern { get; }

    [GeneratedRegex(@"not ready|follows", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 100)]
    private static partial Regex NotReadyPattern { get; }

    [GeneratedRegex(@"^\s*(?!(?:the|a|an|this|that|my|your|our|each|every|which|one|same|new|old|whole)\s)(?<lane>[a-z][a-z-]*)\s+lane\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 100)]
    private static partial Regex AnnouncePattern { get; }
}
