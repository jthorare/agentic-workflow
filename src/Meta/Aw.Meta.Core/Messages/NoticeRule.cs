using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Meta.Core.Messages;

/// <summary>
/// The one rule for a notice: a message that asks nothing, so it opens no reply and never blocks a lane's stop. It is computed from the
/// text (no stored kind). The rules read the first line, after an optional "&lt;lane&gt; -&gt; &lt;lane&gt;:" sender prefix.
/// </summary>
public static partial class NoticeRule
{
    private const int AckMaxBytes = 240;

    // A line that starts with one of these is never a notice: it is a protocol line the receiver acts on.
    private static readonly string[] _protectedPrefixes = ["OK #", "NOT OK #", "DIGEST READY", "OWNER QUESTION", "OWNER ANSWER q"];

    private static readonly string[] _noticePrefixes = ["FYI:", "NOTICE:", "main moved"];

    private static readonly string[] _ackWords = ["ack", "acked", "thanks", "thank you", "noted", "got it", "roger"];

    /// <summary>Whether <paramref name="body"/> is a notice.</summary>
    /// <param name="body">The message text.</param>
    /// <returns>True when the message asks nothing: a lane map or "main moved" line, an FYI or NOTICE, or a short bare ack with no question.</returns>
    public static bool IsNotice(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var text = StripSender(body.Trim());
        var line = FirstLine(text);
        if (IsProtected(line, text) || Asks(body))
        {
            return false;
        }

        return _noticePrefixes.Any(p => line.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            || IsLaneMap(line)
            || IsBareAck(line, body);
    }

    private static bool IsProtected(string line, string text) =>
        _protectedPrefixes.Any(p => line.StartsWith(p, StringComparison.OrdinalIgnoreCase))
        || MessageClassifier.Classify(text).Kind is MessageKind.Ready or MessageKind.OwnerQuestion;

    // A "please" or a "?" anywhere is an ask, even inside an ack, an FYI or a "main moved" line ("Ack: ... please lane-recycle me",
    // meta.db replay; "main moved ... can you merge main?", #2498).
    private static bool Asks(string body) =>
        body.Contains("please", StringComparison.OrdinalIgnoreCase) || body.Contains('?', StringComparison.Ordinal);

    private static bool IsLaneMap(string line) =>
        line.StartsWith("LANE MAP v", StringComparison.OrdinalIgnoreCase) && line.Length > 10 && char.IsAsciiDigit(line[10]);

    private static bool IsBareAck(string line, string body)
    {
        if (Encoding.UTF8.GetByteCount(body) >= AckMaxBytes)
        {
            return false;
        }

        return _ackWords.Any(word => line.StartsWith(word, StringComparison.OrdinalIgnoreCase)
            && (line.Length == word.Length || !char.IsLetterOrDigit(line[word.Length])));
    }

    private static string StripSender(string text)
    {
        try
        {
            var match = SenderPrefix.Match(text);
            return match.Success ? text[match.Length..] : text;
        }
        catch (RegexMatchTimeoutException)
        {
            return text; // a timeout never strips (spec §4c)
        }
    }

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }

    [GeneratedRegex(@"\A[A-Za-z0-9_.-]+\s*->\s*[A-Za-z0-9_.-]+\s*:\s*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex SenderPrefix { get; }
}
