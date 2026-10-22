namespace Aw.CodeIndex.Core.Similarity;

/// <summary>The lexical tokenizer the clones layer reads (spec §5.3): any C-like or scripting language, no parse. Comments are dropped and literals blanked.</summary>
public static class CloneTokenizer
{
    /// <summary>Splits source text into tokens: identifiers and keywords as written, one token per punctuation character, '0' for a number and '"' for a string.</summary>
    /// <param name="text">The source text.</param>
    /// <returns>The tokens in order.</returns>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < text.Length)
        {
            i = Step(text, i, tokens);
        }

        return tokens;
    }

    /// <summary>Tokenizes only the lines of a symbol's span.</summary>
    /// <param name="text">The whole file's text.</param>
    /// <param name="startLine">The first line, 1-based.</param>
    /// <param name="endLine">The last line, inclusive.</param>
    /// <returns>The tokens of those lines; empty when the span lies outside the text.</returns>
    public static IReadOnlyList<string> TokenizeLines(string text, int startLine, int endLine)
    {
        var lines = text.Split('\n');
        var first = Math.Max(startLine, 1);
        var last = Math.Min(endLine, lines.Length);
        return first > last ? [] : Tokenize(string.Join('\n', lines[(first - 1)..last]));
    }

    /// <summary>Reads one token (or skips trivia) at <paramref name="i"/>.</summary>
    /// <returns>The index after what was read.</returns>
    private static int Step(string text, int i, List<string> tokens)
    {
        var afterTrivia = SkipTrivia(text, i);
        if (afterTrivia > i)
        {
            return afterTrivia;
        }

        var c = text[i];
        if (c is '"' or '\'')
        {
            tokens.Add("\"");
            return StringEnd(text, i);
        }

        if (char.IsAsciiDigit(c))
        {
            tokens.Add("0");
            return Skip(text, i, static ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '_');
        }

        if (IsIdentifierStart(c))
        {
            var end = Skip(text, i, static ch => char.IsLetterOrDigit(ch) || ch == '_');
            tokens.Add(text[i..end]);
            return end;
        }

        tokens.Add(c.ToString());
        return i + 1;
    }

    /// <summary>Skips whitespace and comments at <paramref name="i"/>.</summary>
    /// <returns>The index after the trivia, or <paramref name="i"/> when there is none.</returns>
    private static int SkipTrivia(string text, int i)
    {
        var c = text[i];
        if (char.IsWhiteSpace(c))
        {
            return i + 1;
        }

        if (c == '#' || (c == '/' && Next(text, i) == '/'))
        {
            return LineEnd(text, i);
        }

        if (c == '/' && Next(text, i) == '*')
        {
            var close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return close < 0 ? text.Length : close + 2;
        }

        return i;
    }

    private static char Next(string text, int i) => i + 1 < text.Length ? text[i + 1] : '\0';

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c is '_' or '$' or '@';

    private static int LineEnd(string text, int from)
    {
        var end = text.IndexOf('\n', from);
        return end < 0 ? text.Length : end;
    }

    private static int Skip(string text, int from, Func<char, bool> inside)
    {
        var i = from + 1;
        while (i < text.Length && inside(text[i]))
        {
            i++;
        }

        return i;
    }

    /// <summary>The index after the closing quote; an unterminated string runs to the end of the text.</summary>
    private static int StringEnd(string text, int open)
    {
        var quote = text[open];
        var i = open + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
            }
            else if (text[i] == quote)
            {
                return i + 1;
            }
            else
            {
                i++;
            }
        }

        return text.Length;
    }
}
