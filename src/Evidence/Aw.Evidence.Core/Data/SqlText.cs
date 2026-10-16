using System.Buffers;
using System.Text;

namespace Aw.Evidence.Core.Data;

/// <summary>Splits SQL text into statements and words without ever looking inside a quoted string, a quoted identifier or a comment.</summary>
public static class SqlText
{
    private static readonly string[] _readPragmas =
        ["TABLE_INFO", "TABLE_XINFO", "INDEX_LIST", "INDEX_INFO", "INDEX_XINFO", "FOREIGN_KEY_LIST", "DATABASE_LIST", "TABLE_LIST"];

    private static readonly SearchValues<char> _tagChars = SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_");

    /// <summary>Splits <paramref name="sql"/> at its top-level semicolons.</summary>
    /// <param name="sql">The SQL text.</param>
    /// <param name="nestedComments">True when block comments nest (PostgreSQL); false when the first closing marker ends one (SQLite).</param>
    /// <returns>The non-empty statements, comments removed and trimmed, without their semicolons.</returns>
    public static IReadOnlyList<string> Statements(string sql, bool nestedComments) => Scan(sql, split: true, maskQuoted: false, nestedComments);

    /// <summary>The words (letters, digits, underscores) of a statement outside quotes and comments, upper-cased.</summary>
    /// <param name="statement">One statement.</param>
    /// <returns>The words, in order.</returns>
    public static IReadOnlyList<string> Words(string statement)
    {
        var masked = Scan(statement, split: false, maskQuoted: true, nestedComments: true);
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in masked.Count == 0 ? string.Empty : masked[0])
        {
            if (IsWordChar(c))
            {
                _ = word.Append(char.ToUpperInvariant(c));
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                _ = word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        return words;
    }

    /// <summary>True when <paramref name="statement"/> is a PRAGMA that only reads a schema fact (table_info, index_list, …) and assigns nothing.</summary>
    /// <param name="statement">One statement.</param>
    /// <returns>True for a read pragma.</returns>
    public static bool IsReadPragma(string statement) =>
        Words(statement) is ["PRAGMA", var name, ..] && _readPragmas.Contains(name, StringComparer.Ordinal) && !Masked(statement).Contains('=', StringComparison.Ordinal);

    /// <summary>The statement with every quoted body emptied and comments removed, for a check on its punctuation.</summary>
    /// <param name="statement">One statement.</param>
    /// <returns>The masked text.</returns>
    internal static string Masked(string statement) => Scan(statement, split: false, maskQuoted: true, nestedComments: true) is { Count: > 0 } one ? one[0] : string.Empty;

    private static bool IsWordChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '_';

    private static List<string> Scan(string sql, bool split, bool maskQuoted, bool nestedComments)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (CommentEnd(sql, i, nestedComments) is var after and >= 0)
            {
                i = after;
                _ = current.Append(' ');
            }
            else if (c is '\'' or '"' or '`')
            {
                i = Quoted(sql, i, current, maskQuoted, backslash: c == '\'' && AfterEscapeMarker(sql, i));
            }
            else if (c == '$' && DollarTag(sql, i) is { } tag)
            {
                i = Dollar(sql, i, tag, current, maskQuoted);
            }
            else if (c == ';' && split)
            {
                Flush(statements, current);
                i++;
            }
            else
            {
                _ = current.Append(c);
                i++;
            }
        }

        Flush(statements, current);
        return statements;
    }

    private static void Flush(List<string> statements, StringBuilder current)
    {
        var text = current.ToString().Trim();
        _ = current.Clear();
        if (text.Length > 0)
        {
            statements.Add(text);
        }
    }

    // Where the comment starting at i ends; -1 when none starts there.
    private static int CommentEnd(string sql, int i, bool nested) => sql[i] switch
    {
        '-' when Next(sql, i) == '-' => SkipLine(sql, i),
        '/' when Next(sql, i) == '*' => SkipBlock(sql, i, nested),
        _ => -1,
    };

    private static char Next(string sql, int i) => i + 1 < sql.Length ? sql[i + 1] : '\0';

    private static int SkipLine(string sql, int from)
    {
        var end = sql.IndexOf('\n', from);
        return end < 0 ? sql.Length : end + 1;
    }

    // PostgreSQL nests block comments; SQLite ends one at the first closing marker.
    private static int SkipBlock(string sql, int from, bool nested)
    {
        var depth = 0;
        var i = from;
        while (i < sql.Length)
        {
            if (sql[i] == '/' && Next(sql, i) == '*' && (nested || depth == 0))
            {
                depth++;
                i += 2;
            }
            else if (sql[i] == '*' && Next(sql, i) == '/')
            {
                depth--;
                i += 2;
                if (depth == 0)
                {
                    return i;
                }
            }
            else
            {
                i++;
            }
        }

        return sql.Length;
    }

    // E'...' strings take a backslash as an escape.
    private static bool AfterEscapeMarker(string sql, int quote) =>
        quote > 0 && sql[quote - 1] is 'e' or 'E' && (quote == 1 || !IsWordChar(sql[quote - 2]));

    private static int Quoted(string sql, int open, StringBuilder into, bool maskBody, bool backslash)
    {
        var delimiter = sql[open];
        _ = into.Append(delimiter);
        var i = open + 1;
        while (i < sql.Length)
        {
            var c = sql[i];
            if (backslash && c == '\\' && i + 1 < sql.Length)
            {
                AppendBody(into, maskBody, sql.AsSpan(i, 2));
                i += 2;
            }
            else if (c == delimiter && Next(sql, i) == delimiter)
            {
                AppendBody(into, maskBody, sql.AsSpan(i, 2));
                i += 2;
            }
            else if (c == delimiter)
            {
                _ = into.Append(delimiter);
                return i + 1;
            }
            else
            {
                AppendBody(into, maskBody, sql.AsSpan(i, 1));
                i++;
            }
        }

        return i;
    }

    private static void AppendBody(StringBuilder into, bool maskBody, ReadOnlySpan<char> text)
    {
        if (!maskBody)
        {
            _ = into.Append(text);
        }
    }

    // $tag$ or $$ (the tag is an identifier that does not start with a digit), not part of a longer word.
    private static string? DollarTag(string sql, int at)
    {
        if (at > 0 && IsWordChar(sql[at - 1]))
        {
            return null;
        }

        var end = sql.IndexOf('$', at + 1);
        if (end < 0)
        {
            return null;
        }

        var tag = sql.AsSpan(at + 1, end - at - 1);
        return tag.Length == 0 || (!char.IsAsciiDigit(tag[0]) && !tag.ContainsAnyExcept(_tagChars)) ? sql.Substring(at, end - at + 1) : null;
    }

    private static int Dollar(string sql, int open, string tag, StringBuilder into, bool maskBody)
    {
        _ = into.Append(tag);
        var bodyStart = open + tag.Length;
        var close = sql.IndexOf(tag, bodyStart, StringComparison.Ordinal);
        if (close < 0)
        {
            AppendBody(into, maskBody, sql.AsSpan(bodyStart));
            return sql.Length;
        }

        AppendBody(into, maskBody, sql.AsSpan(bodyStart, close - bodyStart));
        _ = into.Append(tag);
        return close + tag.Length;
    }
}
