using System.Globalization;
using System.Text;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>Reads the entries of a located list: where each one starts and ends and what its 'id:' says (see <see cref="ProfileEntryBlock"/>).</summary>
internal static class ProfileEntryScan
{
    /// <summary>One entry of the list.</summary>
    /// <param name="Start">Its '- ' line.</param>
    /// <param name="End">One past its last content line.</param>
    /// <param name="Id">Its 'id:' value, or null when it has none the edit can read.</param>
    /// <param name="Name">Its 'name:' value, or null when it has none the edit can read.</param>
    internal sealed record EntryAt(int Start, int End, string? Id, string? Name);

    /// <summary>The entries of <paramref name="list"/>.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <param name="list">The located list.</param>
    /// <returns>The entries in order, or null when the list has a layout this edit leaves alone.</returns>
    internal static List<EntryAt>? Entries(List<string> lines, ProfileBlockLocator.ListAt list)
    {
        var starts = Starts(lines, list);
        if (starts is null)
        {
            return null;
        }

        var entries = new List<EntryAt>();
        for (var n = 0; n < starts.Count; n++)
        {
            var limit = n + 1 < starts.Count ? starts[n + 1] : list.ContentEnd;
            var end = ProfileBlockLocator.ContentEnd(lines, starts[n] - 1, limit);   // trailing comments and blanks stay with what follows
            entries.Add(new EntryAt(starts[n], end, KeyOf(lines, starts[n], end, "id:"), KeyOf(lines, starts[n], end, "name:")));
        }

        return entries;
    }

    // The '- ' lines at the list's item indent; null for anything the edit can't place: a line outdented or not an item, a flow entry, an anchor or a block scalar.
    private static List<int>? Starts(List<string> lines, ProfileBlockLocator.ListAt list)
    {
        var starts = new List<int>();
        for (var i = list.KeyLine + 1; i < list.ContentEnd; i++)
        {
            if (ProfileBlockLocator.IsBlankOrComment(lines[i]))
            {
                continue;
            }

            var at = YamlProfileLines.Indent(lines[i]);
            var body = at > list.ItemIndent && starts.Count > 0;
            var item = at == list.ItemIndent && ProfileBlockLocator.IsItem(lines[i]) && !FlowEntry(lines[i]);
            if (Unsafe(lines[i]) || !(body || item))
            {
                return null;
            }

            if (item)
            {
                starts.Add(i);
            }
        }

        return starts;
    }

    // '- {...}' or '- [...]': an entry written as a flow value.
    private static bool FlowEntry(string line)
    {
        var body = line.TrimStart(' ')[1..].TrimStart(' ');
        return body.Length > 0 && body[0] is '{' or '[';
    }

    // The entry's scalar under <key> ('id:', 'name:'), or null when it has none or writes it in a way this edit doesn't read.
    private static string? KeyOf(List<string> lines, int start, int end, string key)
    {
        var after = lines[start].TrimStart(' ')[1..];
        var keyIndent = YamlProfileLines.Indent(lines[start]) + 1 + (after.Length - after.TrimStart(' ').Length);
        if (after.Trim().Length == 0)
        {
            keyIndent = end > start + 1 ? YamlProfileLines.Indent(lines[start + 1]) : -1;
        }

        for (var i = start; i < end; i++)
        {
            var text = string.Empty;
            if (i == start)
            {
                text = after.TrimStart(' ');
            }
            else if (!ProfileBlockLocator.IsBlankOrComment(lines[i]) && YamlProfileLines.Indent(lines[i]) == keyIndent)
            {
                text = lines[i].TrimStart(' ');
            }

            if (text.StartsWith(key, StringComparison.Ordinal))
            {
                return Scalar(ProfileBlockLocator.StripComment(text[key.Length..]).Trim());
            }
        }

        return null;
    }

    private static string? Scalar(string token)
    {
        if (token.Length == 0)
        {
            return null;
        }

        return token[0] switch
        {
            '"' => Unescape(token),
            '\'' => token.Length >= 2 && token[^1] == '\'' ? token[1..^1].Replace("''", "'", StringComparison.Ordinal) : null,
            '{' or '[' or '&' or '*' or '|' or '>' or '!' => null,
            _ => token,
        };
    }

    // A double-quoted scalar as ProfileIndexText writes it (and as a hand-written one with the same escapes); any other escape is not read.
    private static string? Unescape(string token)
    {
        if (token.Length < 2 || token[^1] != '"')
        {
            return null;
        }

        var text = new StringBuilder();
        for (var i = 1; i < token.Length - 1; i++)
        {
            if (token[i] != '\\')
            {
                _ = text.Append(token[i]);
                continue;
            }

            i++;
            var escaped = i < token.Length - 1 ? Escape(token, ref i) : null;
            if (escaped is null)
            {
                return null;
            }

            _ = text.Append(escaped);
        }

        return text.ToString();
    }

    private static string? Escape(string token, ref int i) => token[i] switch
    {
        '\\' => "\\",
        '"' => "\"",
        'n' => "\n",
        'r' => "\r",
        't' => "\t",
        'u' when i + 4 < token.Length && int.TryParse(token.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) => Advance(ref i, 4, ((char)code).ToString()),
        _ => null,
    };

    private static string Advance(ref int i, int by, string value)
    {
        i += by;
        return value;
    }

    // A value that starts an anchor, an alias, a tag or a block scalar: the lines under it can't be told apart by indent alone.
    private static bool Unsafe(string line)
    {
        var text = ProfileBlockLocator.StripComment(line.TrimStart(' ')).Trim();
        while (text.StartsWith("- ", StringComparison.Ordinal))
        {
            text = text[2..].TrimStart(' ');
        }

        if (text.Length > 0 && text[0] is not ('"' or '\'') && text.IndexOf(": ", StringComparison.Ordinal) is var colon and >= 0)
        {
            text = text[(colon + 2)..].TrimStart(' ');
        }

        return text.Length > 0 && text[0] is '&' or '*' or '|' or '>' or '!';
    }
}
