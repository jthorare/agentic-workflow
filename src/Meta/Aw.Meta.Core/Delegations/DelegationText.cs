using System.Globalization;

namespace Aw.Meta.Core.Delegations;

/// <summary>The words of a delegation: the ACTION message, the issue footer, and how text is matched against an issue number or a keyword.</summary>
public static class DelegationText
{
    /// <summary>The last line of the ACTION message.</summary>
    public const string Yours = "Yours: take it in your lane and reply with the PR number.";

    /// <summary>The ACTION message: "ACTION: #n title", the issue link, then <see cref="Yours"/>.</summary>
    /// <param name="issue">The issue number.</param>
    /// <param name="title">The issue title.</param>
    /// <param name="url">The issue link.</param>
    /// <returns>The three lines.</returns>
    public static string Message(int issue, string title, string url) =>
        string.Create(CultureInfo.InvariantCulture, $"ACTION: #{issue} {title}\n{url}\n{Yours}");

    /// <summary>The line that ends a delegated issue's body.</summary>
    /// <param name="lane">The lane.</param>
    /// <returns>The footer.</returns>
    public static string Footer(string lane) => $"Delegated to lane {lane} by meta.";

    /// <summary>Whether <paramref name="text"/> names the issue as "#n", with no digit right after it ("#12" is not named by "#123").</summary>
    /// <param name="text">The message text.</param>
    /// <param name="issue">The issue number.</param>
    /// <returns>True when it names the issue.</returns>
    public static bool Names(string text, int issue)
    {
        ArgumentNullException.ThrowIfNull(text);
        var marker = "#" + issue.ToString(CultureInfo.InvariantCulture);
        for (var at = text.IndexOf(marker, StringComparison.Ordinal); at >= 0; at = text.IndexOf(marker, at + 1, StringComparison.Ordinal))
        {
            var end = at + marker.Length;
            if (end >= text.Length || !char.IsAsciiDigit(text[end]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="text"/> holds <paramref name="keyword"/> as a whole word, in any case: no letter, digit or underscore right before or after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="keyword">The keyword.</param>
    /// <returns>True when it holds it.</returns>
    public static bool HasWord(string text, string keyword)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(keyword);
        var word = keyword.Trim();
        if (word.Length == 0)
        {
            return false;
        }

        for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + word.Length;
            if ((at == 0 || !IsWordChar(text[at - 1])) && (end >= text.Length || !IsWordChar(text[end])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';
}
