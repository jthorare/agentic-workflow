namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The text edit that adds one list entry under 'sandbox.repos.&lt;repo&gt;.&lt;list&gt;' in the profile, line by line, so every other key and comment stays as it was. A missing 'sandbox', 'repos', repo or list key is created at the end of
/// its parent's block; an empty one written '{}' or '[]' becomes a block; a block list grows after its last entry at the indent it already uses. A key written any other way (a flow list with entries, null) is not edited: the caller is told
/// to add the lines by hand.
/// </summary>
internal static class ProfileSandboxText
{
    /// <summary>Adds <paramref name="item"/> to the repo's list.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="repo">The repo's name.</param>
    /// <param name="list">'hosts' or 'homeAllow'.</param>
    /// <param name="item">The entry's lines, the first starting with '- ', without indent.</param>
    /// <returns>The new text, or null when the file's layout is one this edit leaves alone.</returns>
    internal static string? Add(string text, string repo, string list, IReadOnlyList<string> item)
    {
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";   // the file keeps the line ending it has
        var lines = new List<string>(text.ReplaceLineEndings("\n").Split('\n'));
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);   // the final newline, put back on write
        }

        var at = -1;
        foreach (var (key, isList) in new[] { ("sandbox", false), ("repos", false), (repo, false), (list, true) })
        {
            at = Ensure(lines, at, key, isList);
            if (at < 0)
            {
                return null;
            }
        }

        var end = BlockEnd(lines, at);
        var indent = ItemIndent(lines, at, end);
        lines.InsertRange(end, item.Select(l => new string(' ', indent) + l));
        return string.Join(eol, lines) + eol;
    }

    // The line of <key> under <parent> (-1 is the file), created at the end of the parent's block when it is missing; -1 when the key is written in a way this edit leaves alone.
    private static int Ensure(List<string> lines, int parent, string key, bool isList)
    {
        var found = Find(lines, parent, key);
        if (found < 0)
        {
            var pad = new string(' ', ChildIndent(lines, parent));
            var position = parent < 0 ? lines.Count : BlockEnd(lines, parent);
            lines.Insert(position, $"{pad}{key}:");
            return position;
        }

        var rest = Rest(lines[found]);
        if (rest == "")
        {
            return found;
        }

        if (rest == (isList ? "[]" : "{}"))
        {
            lines[found] = lines[found][..(lines[found].IndexOf(':', StringComparison.Ordinal) + 1)];
            return found;
        }

        return -1;
    }

    // The child of <parent> named <key>: the lines one level in, at the indent of the first of them.
    private static int Find(List<string> lines, int parent, string key)
    {
        var parentIndent = parent < 0 ? -1 : Indent(lines[parent]);
        int? childIndent = null;
        for (var i = parent + 1; i < lines.Count; i++)
        {
            if (IsBlank(lines[i]))
            {
                continue;
            }

            var indent = Indent(lines[i]);
            if (indent <= parentIndent)
            {
                break;
            }

            childIndent ??= indent;
            if (indent == childIndent && string.Equals(NameOf(lines[i]), key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int ChildIndent(List<string> lines, int parent)
    {
        var parentIndent = parent < 0 ? -1 : Indent(lines[parent]);
        for (var i = parent + 1; i < lines.Count; i++)
        {
            if (!IsBlank(lines[i]))
            {
                return Indent(lines[i]) > parentIndent ? Indent(lines[i]) : parentIndent + 2;
            }
        }

        return parentIndent + 2;
    }

    // One past the last content line that belongs to the key's block: lines indented deeper, or '- ' lines at its own indent.
    private static int BlockEnd(List<string> lines, int key)
    {
        var keyIndent = Indent(lines[key]);
        var end = key + 1;
        for (var i = key + 1; i < lines.Count; i++)
        {
            if (IsBlank(lines[i]))
            {
                continue;
            }

            var trimmed = lines[i].TrimStart();
            if (Indent(lines[i]) > keyIndent || (Indent(lines[i]) == keyIndent && (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed == "-")))
            {
                end = i + 1;
                continue;
            }

            break;
        }

        return end;
    }

    private static int ItemIndent(List<string> lines, int key, int end)
    {
        for (var i = key + 1; i < end; i++)
        {
            if (lines[i].TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                return Indent(lines[i]);
            }
        }

        return Indent(lines[key]) + 2;
    }

    // The key a line names, unquoted, or null for a list entry or anything that isn't 'key:'.
    private static string? NameOf(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith('#') || trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed[0] is '"' or '\'')
        {
            var close = trimmed.IndexOf(trimmed[0], 1);
            return close > 0 && trimmed[(close + 1)..].StartsWith(':') ? trimmed[1..close] : null;
        }

        var colon = trimmed.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && (colon == trimmed.Length - 1 || trimmed[colon + 1] == ' ') ? trimmed[..colon] : null;
    }

    // What follows the key's colon, without a trailing comment.
    private static string Rest(string line)
    {
        var trimmed = line.TrimStart();
        var colon = trimmed[0] is '"' or '\'' ? trimmed.IndexOf(':', trimmed.IndexOf(trimmed[0], 1)) : trimmed.IndexOf(':', StringComparison.Ordinal);
        var rest = trimmed[(colon + 1)..];
        var hash = rest.IndexOf(" #", StringComparison.Ordinal);
        return (hash >= 0 ? rest[..hash] : rest).Trim();
    }

    private static bool IsBlank(string line) => line.Trim().Length == 0 || line.TrimStart().StartsWith('#');

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
}
