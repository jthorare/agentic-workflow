using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.Library;

/// <summary>
/// Reads the top-level keys of a design doc's front matter: scalars (plain, single or double quoted), flow lists and block lists. Anything deeper
/// (a nested map, a block scalar's lines) is skipped. The convention's keys are flat, and the parser needs the raw text of <c language="text">aw-doc</c>
/// (YAML reads <c language="text">1.0</c> as the number 1), so a general YAML reader would add a package without adding anything.
/// </summary>
internal static partial class FrontMatterReader
{
    /// <summary>Reads the keys between two line indexes.</summary>
    /// <param name="lines">The file's lines.</param>
    /// <param name="from">The first line index of the block.</param>
    /// <param name="to">The index after the last line of the block.</param>
    /// <returns>The entries in order.</returns>
    internal static IReadOnlyList<YamlEntry> Read(IReadOnlyList<string> lines, int from, int to)
    {
        var entries = new List<YamlEntry>();
        var i = from;
        while (i < to)
        {
            var match = KeyLine.Match(lines[i]);
            if (!match.Success)
            {
                i++;
                continue;
            }

            var rest = match.Groups["rest"].Success ? match.Groups["rest"].Value.Trim() : string.Empty;
            var children = new List<string>();
            var next = i + 1;
            while (next < to && (lines[next].Length == 0 || char.IsWhiteSpace(lines[next][0]) || lines[next].StartsWith("- ", StringComparison.Ordinal)))
            {
                children.Add(lines[next]);
                next++;
            }

            entries.Add(Entry(match.Groups["key"].Value, i + 1, rest, children));
            i = next;
        }

        return entries;
    }

    /// <summary>Reads one value: a quoted string, or a plain one up to a trailing comment.</summary>
    /// <param name="text">The value text.</param>
    /// <returns>The scalar.</returns>
    internal static YamlScalar Scalar(string text)
    {
        var t = text.Trim();
        if (t.Length > 0 && t[0] is '"' or '\'')
        {
            return new YamlScalar(Unquote(t), Quoted: true);
        }

        var comment = t.StartsWith('#') ? 0 : t.IndexOf(" #", StringComparison.Ordinal);
        return new YamlScalar((comment < 0 ? t : t[..comment]).Trim(), Quoted: false);
    }

    private static YamlEntry Entry(string key, int line, string rest, List<string> children)
    {
        if (rest.StartsWith('['))
        {
            var joined = children.Count == 0 || rest.Contains(']', StringComparison.Ordinal) ? rest : rest + " " + string.Join(' ', children.Select(static c => c.Trim()));
            return new YamlEntry(key, line, Scalar: null, List: FlowItems(joined));
        }

        if (rest.Length > 0 && rest[0] is '|' or '>')
        {
            return new YamlEntry(key, line, new YamlScalar(string.Join(' ', children.Select(static c => c.Trim()).Where(static c => c.Length > 0)), Quoted: false), List: null);
        }

        if (rest.Length == 0 || rest[0] == '#')
        {
            var items = children.Select(static c => BlockItem.Match(c)).Where(static m => m.Success).Select(static m => Scalar(m.Groups["item"].Value)).ToList();
            return items.Count > 0 ? new YamlEntry(key, line, Scalar: null, List: items) : new YamlEntry(key, line, new YamlScalar(string.Empty, Quoted: false), List: null);
        }

        return new YamlEntry(key, line, Scalar(rest), List: null);
    }

    private static List<YamlScalar> FlowItems(string text)
    {
        var open = text.IndexOf('[', StringComparison.Ordinal);
        var close = text.LastIndexOf(']');
        var inner = close > open ? text[(open + 1)..close] : text[(open + 1)..];
        var items = new List<YamlScalar>();
        var current = new StringBuilder();
        var quote = '\0';
        foreach (var c in inner)
        {
            if (quote != '\0')
            {
                _ = current.Append(c);
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
                _ = current.Append(c);
            }
            else if (c == ',')
            {
                items.Add(Scalar(current.ToString()));
                _ = current.Clear();
            }
            else
            {
                _ = current.Append(c);
            }
        }

        if (current.ToString().Trim().Length > 0)
        {
            items.Add(Scalar(current.ToString()));
        }

        return items;
    }

    private static string Unquote(string t)
    {
        var quote = t[0];
        var value = new StringBuilder();
        for (var i = 1; i < t.Length; i++)
        {
            var c = t[i];
            if (quote == '"' && c == '\\' && i + 1 < t.Length)
            {
                i++;
                _ = value.Append(t[i]);
            }
            else if (quote == '\'' && c == '\'' && i + 1 < t.Length && t[i + 1] == '\'')
            {
                i++;
                _ = value.Append('\'');
            }
            else if (c == quote)
            {
                break;
            }
            else
            {
                _ = value.Append(c);
            }
        }

        return value.ToString();
    }

    [GeneratedRegex("^(?<key>[A-Za-z0-9_.-]+):(?:[ \\t]+(?<rest>.*))?\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyLine { get; }

    [GeneratedRegex("^\\s*-[ \\t]+(?<item>.*)\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BlockItem { get; }
}
