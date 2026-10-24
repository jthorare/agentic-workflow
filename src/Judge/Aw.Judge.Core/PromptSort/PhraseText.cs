using System.Text;

namespace Aw.Judge.Core.PromptSort;

/// <summary>Lower-cased prompt text with whitespace runs collapsed, searched for whole phrases (a word boundary on each side, any whitespace between words).</summary>
internal sealed class PhraseText
{
    internal PhraseText(string lowerCased)
    {
        Text = Collapse(lowerCased);
    }

    internal string Text { get; }

    internal bool Has(string phrase)
    {
        for (var at = Text.IndexOf(phrase, StringComparison.Ordinal); at >= 0; at = Text.IndexOf(phrase, at + 1, StringComparison.Ordinal))
        {
            var end = at + phrase.Length;
            if ((at == 0 || !IsWord(Text[at - 1])) && (end == Text.Length || !IsWord(Text[end])))
            {
                return true;
            }
        }

        return false;
    }

    internal bool HasAny(IEnumerable<string> phrases) => phrases.Any(Has);

    internal int Count(IEnumerable<string> phrases) => phrases.Count(Has);

    private static bool IsWord(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static string Collapse(string text)
    {
        var collapsed = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = collapsed.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                _ = collapsed.Append(' ');
                pendingSpace = false;
            }

            _ = collapsed.Append(c);
        }

        return collapsed.ToString();
    }
}
