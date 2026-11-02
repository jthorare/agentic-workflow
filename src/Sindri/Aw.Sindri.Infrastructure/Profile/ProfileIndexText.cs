using System.Text;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The text edit behind <see cref="YamlProfileIndexWriter"/>: sets and removes entries of the top-level 'index.concepts', and of the top-level or a repo's own 'index.pages', by id, line by line (see <see cref="ProfileEntryBlock"/>), so every other key and comment stays
/// as it was and the file keeps its line ending. An entry is written with 'id' first; a null field is left out and an empty list is written as '[]', so what is read back is what was set.
/// </summary>
internal static class ProfileIndexText
{
    private static readonly string[] _keys = ["index", "concepts"];
    private static readonly string[] _pageKeys = ["index", "pages"];
    private static readonly ProfileEntryBlock.ListPath _concepts = new(_keys);

    /// <summary>The entry as block lines at <paramref name="indent"/>.</summary>
    /// <param name="id">The entry's id.</param>
    /// <param name="entry">The entry; its own Id is ignored.</param>
    /// <param name="indent">The indent of the '- '.</param>
    /// <returns>The lines.</returns>
    internal static string[] Entry(string id, ConceptSettings entry, int indent)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var pad = new string(' ', indent);
        var lines = new List<string> { $"{pad}- id: {Quote(id)}" };
        Scalar(lines, pad, "name", entry.Name);
        Scalar(lines, pad, "summary", entry.Summary);
        List(lines, pad, "aliases", entry.Aliases);
        List(lines, pad, "features", entry.Features);
        List(lines, pad, "paths", entry.Paths);
        List(lines, pad, "dependsOn", entry.DependsOn);
        List(lines, pad, "owns", entry.Owns);
        return [.. lines];
    }

    /// <summary>The page as block lines at <paramref name="indent"/>.</summary>
    /// <param name="id">The page's id.</param>
    /// <param name="page">The page; its own Id is ignored.</param>
    /// <param name="indent">The indent of the '- '.</param>
    /// <returns>The lines.</returns>
    internal static string[] Entry(string id, PageSettings page, int indent)
    {
        ArgumentNullException.ThrowIfNull(page);
        var pad = new string(' ', indent);
        var lines = new List<string> { $"{pad}- id: {Quote(id)}" };
        Scalar(lines, pad, "name", page.Name);
        Scalar(lines, pad, "repo", page.Repo);
        Scalar(lines, pad, "surface", page.Surface);
        if (page.Actions is { } actions)
        {
            lines.Add(actions.Count == 0 ? $"{pad}  actions: []" : $"{pad}  actions:");
            lines.AddRange(actions.SelectMany(a => Action(a, indent + 4)));
        }

        return [.. lines];
    }

    /// <summary>Where an edit's list is, for a person to make it by hand: 'index.concepts', 'index.pages' or 'repos.web.index.pages'.</summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The key path.</returns>
    internal static string Where(IndexEdit edit) => edit switch
    {
        PageEdit { Repo: { } repo } => $"repos.{repo}.index.pages",
        PageEdit => "index.pages",
        _ => "index.concepts",
    };

    /// <summary>The edit as one line for a refusal: the entry's lines joined, or what to remove.</summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The line.</returns>
    internal static string Describe(IndexEdit edit) => edit switch
    {
        ConceptEdit { Entry: { } entry } c => string.Join(" / ", Entry(c.Id.Value, entry, 4)),
        PageEdit { Entry: { } entry } p => string.Join(" / ", Entry(p.Id.Value, entry, 4)),
        ConceptEdit c => $"remove the entry with id {c.Id.Value}",
        PageEdit p => $"remove the entry with id {p.Id.Value}",
        _ => throw new ArgumentOutOfRangeException(nameof(edit), edit, "an index edit is a concept or a page"),
    };

    /// <summary>Applies <paramref name="edits"/> to <paramref name="text"/> in order.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="edits">The changes.</param>
    /// <returns>The new text, or null when the file's 'index.concepts' has a layout this edit leaves alone.</returns>
    internal static string? Apply(string text, IReadOnlyList<IndexEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = new List<string>(text.ReplaceLineEndings("\n").Split('\n'));
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        foreach (var edit in edits)
        {
            if (!Apply(lines, edit))
            {
                return null;
            }
        }

        return string.Join(eol, lines) + eol;
    }

    private static bool Apply(List<string> lines, IndexEdit edit) => edit switch
    {
        ConceptEdit { Entry: { } entry } c => ProfileEntryBlock.Set(lines, _concepts, c.Id.Value, indent => Entry(c.Id.Value, entry, indent), c.MatchName),
        ConceptEdit c => ProfileEntryBlock.Remove(lines, _concepts, c.Id.Value, c.MatchName),
        PageEdit { Entry: { } entry } p => ProfileEntryBlock.Set(lines, new(_pageKeys, p.Repo), p.Id.Value, indent => Entry(p.Id.Value, entry, indent)),
        PageEdit p => ProfileEntryBlock.Remove(lines, new(_pageKeys, p.Repo), p.Id.Value),
        _ => throw new ArgumentOutOfRangeException(nameof(edit), edit, "an index edit is a concept or a page"),
    };

    // One action of a page, its '- ' at <indent>.
    private static List<string> Action(PageActionSettings action, int indent)
    {
        var pad = new string(' ', indent);
        var lines = new List<string>();
        Scalar(lines, pad, "id", action.Id?.Value);
        Scalar(lines, pad, "name", action.Name);
        Scalar(lines, pad, "action", action.Action);
        List(lines, pad, "concepts", action.Concepts);
        if (lines.Count == 0)
        {
            return [$"{pad}- {{}}"];   // never written: the write rules want a name
        }

        lines[0] = $"{pad}- {lines[0].TrimStart(' ')}";
        return lines;
    }

    private static void Scalar(List<string> lines, string pad, string key, string? value)
    {
        if (value is not null)
        {
            lines.Add($"{pad}  {key}: {Quote(value)}");
        }
    }

    private static void List(List<string> lines, string pad, string key, IReadOnlyList<string>? values)
    {
        if (values is null)
        {
            return;
        }

        if (values.Count == 0)
        {
            lines.Add($"{pad}  {key}: []");
            return;
        }

        lines.Add($"{pad}  {key}:");
        lines.AddRange(values.Select(v => $"{pad}    - {Quote(v)}"));
    }

    // Always double-quoted, so 'true', '123' or 'null' stay strings.
    private static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        foreach (var c in value)
        {
            _ = c switch
            {
                '\\' => text.Append("\\\\"),
                '"' => text.Append("\\\""),
                '\n' => text.Append("\\n"),
                '\r' => text.Append("\\r"),
                '\t' => text.Append("\\t"),
                _ when char.IsControl(c) => text.Append(System.Globalization.CultureInfo.InvariantCulture, $"\\u{(int)c:x4}"),
                _ => text.Append(c),
            };
        }

        return text.Append('"').ToString();
    }
}
