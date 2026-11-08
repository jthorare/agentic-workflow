using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Expressions;

/// <summary>Splits a condition into tokens; every token knows its 1-based column.</summary>
internal static class Tokenizer
{
    internal enum Kind
    {
        Ref,
        Str,
        Num,
        True,
        False,
        Null,
        And,
        Or,
        Not,
        Exists,
        LParen,
        RParen,
        Eq,
        Ne,
        Lt,
        Le,
        Gt,
        Ge,
        End,
    }

    /// <summary>One token: its kind, the source text, the 1-based column and the value of a literal.</summary>
    internal readonly record struct Token(Kind Kind, string Text, int Column, JsonNode? Value = null);

    internal static string Describe(Token token) =>
        token.Kind == Kind.End ? "the end of the expression" : "'" + token.Text + "'";

    internal static string At(int column, string message) =>
        "column " + column.ToString(CultureInfo.InvariantCulture) + ": " + message;

    /// <summary>Tokenizes the text; the last token is always <see cref="Kind.End"/>.</summary>
    internal static bool TryTokenize(string text, out List<Token> tokens, out string? error)
    {
        tokens = [];
        error = null;
        var i = 0;
        while (i < text.Length)
        {
            if (!TryScan(text, ref i, tokens, out error))
            {
                return false;
            }
        }

        tokens.Add(new Token(Kind.End, string.Empty, text.Length + 1));
        return true;
    }

    private static bool TryScan(string text, ref int i, List<Token> tokens, out string? error)
    {
        error = null;
        var c = text[i];
        if (char.IsWhiteSpace(c))
        {
            i++;
            return true;
        }

        if (c == '{')
        {
            return TryBraced(text, ref i, tokens, out error);
        }

        if (c == '"')
        {
            return TryString(text, ref i, tokens, out error);
        }

        if (char.IsAsciiDigit(c) || (c == '-' && i + 1 < text.Length && char.IsAsciiDigit(text[i + 1])))
        {
            Number(text, ref i, tokens);
            return true;
        }

        if (IsIdentStart(c))
        {
            Word(text, ref i, tokens);
            return true;
        }

        if (TrySymbol(text, ref i, tokens))
        {
            return true;
        }

        error = At(i + 1, "unexpected '" + c + "'; a condition uses references, \"strings\", numbers, true, false, null, comparisons, and, or, not, exists() and parentheses");
        return false;
    }

    private static bool IsIdentStart(char c) => char.IsAsciiLetter(c) || c == '_';

    private static bool IsIdentPart(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-';

    private static bool TryBraced(string text, ref int i, List<Token> tokens, out string? error)
    {
        error = null;
        var column = i + 1;
        if (i + 1 >= text.Length || text[i + 1] != '{')
        {
            error = At(column, "unexpected '{'; wrap a reference as {{node.field}} (two braces)");
            return false;
        }

        var close = text.IndexOf("}}", i + 2, StringComparison.Ordinal);
        if (close < 0)
        {
            error = At(column, "'{{' is never closed; end the reference with '}}', like {{node.field}}");
            return false;
        }

        var name = text[(i + 2)..close].Trim();
        if (name.Length == 0 || !IsIdentStart(name[0]) || !name.All(IsIdentPart))
        {
            error = At(column, "expected a reference between the braces, like {{node.field}}");
            return false;
        }

        tokens.Add(new Token(Kind.Ref, name, column));
        i = close + 2;
        return true;
    }

    private static bool TryString(string text, ref int i, List<Token> tokens, out string? error)
    {
        error = null;
        var start = i;
        var value = new StringBuilder();
        i++;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"')
            {
                i++;
                tokens.Add(new Token(Kind.Str, text[start..i], start + 1, Value: JsonValue.Create(value.ToString())));
                return true;
            }

            if (c == '\\')
            {
                var mapped = Unescape(i + 1 < text.Length ? text[i + 1] : '\0');
                if (mapped == '\0')
                {
                    error = At(i + 1, "unknown escape; use \\\" for a quote, \\\\ for a backslash, \\n or \\t");
                    return false;
                }

                value.Append(mapped);
                i += 2;
            }
            else
            {
                value.Append(c);
                i++;
            }
        }

        error = At(start + 1, "the string is never closed; end it with a double quote");
        return false;
    }

    private static char Unescape(char escaped) => escaped switch
    {
        '"' => '"',
        '\\' => '\\',
        'n' => '\n',
        't' => '\t',
        _ => '\0',
    };

    private static void Number(string text, ref int i, List<Token> tokens)
    {
        var start = i;
        i++;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        if (i + 1 < text.Length && text[i] == '.' && char.IsAsciiDigit(text[i + 1]))
        {
            i++;
            while (i < text.Length && char.IsAsciiDigit(text[i]))
            {
                i++;
            }
        }

        var raw = text[start..i];
        var number = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        tokens.Add(new Token(Kind.Num, raw, start + 1, Value: JsonValue.Create(number)));
    }

    private static void Word(string text, ref int i, List<Token> tokens)
    {
        var start = i;
        while (i < text.Length && IsIdentPart(text[i]))
        {
            i++;
        }

        var word = text[start..i];
        var kind = word switch
        {
            "and" => Kind.And,
            "or" => Kind.Or,
            "not" => Kind.Not,
            "true" => Kind.True,
            "false" => Kind.False,
            "null" => Kind.Null,
            "exists" when NextNonSpace(text, i) == '(' => Kind.Exists,
            _ => Kind.Ref,
        };
        tokens.Add(new Token(kind, word, start + 1));
    }

    private static char NextNonSpace(string text, int from)
    {
        while (from < text.Length && char.IsWhiteSpace(text[from]))
        {
            from++;
        }

        return from < text.Length ? text[from] : '\0';
    }

    private static bool TrySymbol(string text, ref int i, List<Token> tokens)
    {
        var c = text[i];
        var next = i + 1 < text.Length ? text[i + 1] : '\0';
        (Kind Kind, int Length)? symbol = c switch
        {
            '(' => (Kind.LParen, 1),
            ')' => (Kind.RParen, 1),
            '=' when next == '=' => (Kind.Eq, 2),
            '!' when next == '=' => (Kind.Ne, 2),
            '<' when next == '=' => (Kind.Le, 2),
            '<' => (Kind.Lt, 1),
            '>' when next == '=' => (Kind.Ge, 2),
            '>' => (Kind.Gt, 1),
            _ => null,
        };
        if (symbol is not { } s)
        {
            return false;
        }

        tokens.Add(new Token(s.Kind, text.Substring(i, s.Length), i + 1));
        i += s.Length;
        return true;
    }
}
