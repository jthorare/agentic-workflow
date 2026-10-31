using System.Text;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The text edit behind <see cref="YamlProfileConceptWriter"/>: sets and removes entries of the top-level 'index.concepts' by id, line by line (see <see cref="ProfileEntryBlock"/>), so every other key and comment stays
/// as it was and the file keeps its line ending. An entry is written with 'id' first; a null field is left out and an empty list is written as '[]', so what is read back is what was set.
/// </summary>
internal static class ProfileConceptText
{
    private static readonly string[] _path = ["index", "concepts"];

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

    /// <summary>Applies <paramref name="edits"/> to <paramref name="text"/> in order.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="edits">The changes.</param>
    /// <returns>The new text, or null when the file's 'index.concepts' has a layout this edit leaves alone.</returns>
    internal static string? Apply(string text, IReadOnlyList<ConceptEdit> edits)
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
            var id = edit.Id.Value;
            var done = edit.Entry is { } entry
                ? ProfileEntryBlock.Set(lines, _path, id, indent => Entry(id, entry, indent), edit.MatchName)
                : ProfileEntryBlock.Remove(lines, _path, id, edit.MatchName);
            if (!done)
            {
                return null;
            }
        }

        return string.Join(eol, lines) + eol;
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
