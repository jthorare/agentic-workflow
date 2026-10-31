namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>Finds the list at a key path in the profile's lines, or adds the keys that are missing (see <see cref="ProfileEntryBlock"/>).</summary>
internal static class ProfileBlockLocator
{
    private const int Step = 2;

    /// <summary>How a key path met the file.</summary>
    internal enum Found
    {
        Yes,
        Missing,
        Created,
        Refused,
    }

    /// <summary>Where the list is.</summary>
    /// <param name="KeyLine">The line of the list's key.</param>
    /// <param name="KeyIndent">The key's indent.</param>
    /// <param name="ItemIndent">The indent of the '- ' lines (the key's plus two when there are none).</param>
    /// <param name="ContentEnd">One past the last content line of the list.</param>
    internal sealed record ListAt(int KeyLine, int KeyIndent, int ItemIndent, int ContentEnd);

    /// <summary>Whether a line is indented with a tab, which this edit won't touch.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <returns>True when one is.</returns>
    internal static bool HasTab(List<string> lines) => lines.Exists(static l => l.TrimStart(' ').StartsWith('\t'));

    /// <summary>Walks the key path. With <paramref name="create"/>, a missing key (and the keys below it) is added together with the entry and Created is returned.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <param name="keyPath">The keys down to the list.</param>
    /// <param name="create">The entry's lines for an indent, or null to only look.</param>
    /// <param name="list">Where the list is, when Yes.</param>
    /// <returns>How the path met the file.</returns>
    internal static Found Locate(List<string> lines, string[] keyPath, Func<int, string[]>? create, out ListAt list)
    {
        list = new ListAt(0, 0, 0, 0);
        var (parent, from, to) = (-1, 0, lines.Count);
        for (var level = 0; level < keyPath.Length; level++)
        {
            var last = level == keyPath.Length - 1;
            var at = FindKey(lines, parent, from, to, keyPath[level]);
            if (at < 0)
            {
                return create is null ? Found.Missing : Add(lines, keyPath[level..], (parent, from, to), create);
            }

            if (!OpenValue(lines, at, keyPath[level], last))
            {
                return Found.Refused;
            }

            var indent = YamlProfileLines.Indent(lines[at]);
            var contentEnd = ContentEnd(lines, at, RangeEnd(lines, at, indent, sequenceAtKeyIndent: last));
            if (last)
            {
                list = new ListAt(at, indent, ItemIndentOf(lines, at + 1, contentEnd, indent), contentEnd);
                return Found.Yes;
            }

            (parent, from, to) = (at, at + 1, contentEnd);
        }

        return Found.Refused;
    }

    internal static int ContentEnd(List<string> lines, int key, int rangeEnd)
    {
        for (var i = rangeEnd - 1; i > key; i--)
        {
            if (!IsBlankOrComment(lines[i]))
            {
                return i + 1;
            }
        }

        return key + 1;
    }

    internal static bool IsItem(string line)
    {
        var text = line.TrimStart(' ');
        return text.StartsWith("- ", StringComparison.Ordinal) || text == "-";
    }

    internal static bool IsBlankOrComment(string line)
    {
        var text = line.Trim();
        return text.Length == 0 || text[0] == '#';
    }

    internal static string StripComment(string text)
    {
        var hash = text.StartsWith('#') ? 0 : text.IndexOf(" #", StringComparison.Ordinal);
        return hash >= 0 ? text[..hash] : text;
    }

    // The key at the head of `keys` is missing from the parent's block: add it, the keys below it and the entry at the end of that block.
    private static Found Add(List<string> lines, string[] keys, (int Parent, int From, int To) scope, Func<int, string[]> create)
    {
        int keyIndent;
        int insertAt;
        if (scope.Parent < 0)
        {
            keyIndent = 0;
            insertAt = lines.Count;
            while (insertAt > 0 && lines[insertAt - 1].Trim().Length == 0)
            {
                insertAt--;
            }
        }
        else
        {
            var first = FirstContentIndent(lines, scope.From, scope.To);
            keyIndent = first >= 0 ? first : YamlProfileLines.Indent(lines[scope.Parent]) + Step;
            insertAt = scope.To;
        }

        var added = new List<string>();
        for (var i = 0; i < keys.Length; i++)
        {
            added.Add(new string(' ', keyIndent + (i * Step)) + keys[i] + ":");
        }

        added.AddRange(create(keyIndent + (keys.Length * Step)));
        lines.InsertRange(insertAt, added);
        return Found.Created;
    }

    private static int FirstContentIndent(List<string> lines, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (!IsBlankOrComment(lines[i]))
            {
                return YamlProfileLines.Indent(lines[i]);
            }
        }

        return -1;
    }

    // The line of `key` among the parent's own children (the top level has indent 0, a deeper level the indent of its first child).
    private static int FindKey(List<string> lines, int parent, int from, int to, string key)
    {
        var indent = parent < 0 ? 0 : FirstContentIndent(lines, from, to);
        for (var i = from; indent >= 0 && i < to; i++)
        {
            if (!IsBlankOrComment(lines[i]) && YamlProfileLines.Indent(lines[i]) == indent && lines[i].AsSpan(indent).StartsWith(key + ":", StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // The key must have no inline value: a list key may say '[]' and a map key '{}', which become the empty block; anything else (a flow value with content, null) is refused.
    private static bool OpenValue(List<string> lines, int at, string key, bool list)
    {
        var rest = StripComment(lines[at].TrimStart(' ')[(key.Length + 1)..]).Trim();
        if (rest.Length > 0 && rest != (list ? "[]" : "{}"))
        {
            return false;
        }

        if (rest.Length > 0)
        {
            lines[at] = new string(' ', YamlProfileLines.Indent(lines[at])) + key + ":";
        }

        return true;
    }

    // One past the last line the key's block can hold: the lines deeper than the key, and, for the list, '- ' lines at the key's own indent.
    private static int RangeEnd(List<string> lines, int key, int indent, bool sequenceAtKeyIndent)
    {
        for (var i = key + 1; i < lines.Count; i++)
        {
            if (IsBlankOrComment(lines[i]))
            {
                continue;
            }

            var at = YamlProfileLines.Indent(lines[i]);
            if (at < indent || (at == indent && !(sequenceAtKeyIndent && IsItem(lines[i]))))
            {
                return i;
            }
        }

        return lines.Count;
    }

    private static int ItemIndentOf(List<string> lines, int from, int to, int keyIndent)
    {
        for (var i = from; i < to; i++)
        {
            if (IsItem(lines[i]))
            {
                return YamlProfileLines.Indent(lines[i]);
            }
        }

        return keyIndent + Step;
    }
}
