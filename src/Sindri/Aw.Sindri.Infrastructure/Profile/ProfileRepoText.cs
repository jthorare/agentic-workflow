using System.Text.Json;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The text edit that adds one entry to the profile's 'repos:' list, line by line, so every other key and comment stays as it was. An empty 'repos: []' becomes a block, a block grows after its last entry
/// (at the indent the file already uses), and a file with no 'repos:' gets one at the end. A 'repos:' written any other way (a flow list with entries, null) is not edited: the caller is told to add the lines by hand.
/// </summary>
internal static class ProfileRepoText
{
    /// <summary>The entry as two block lines at <paramref name="indent"/>.</summary>
    internal static string[] Entry(string name, string path, int indent)
    {
        var pad = new string(' ', indent);
        return [$"{pad}- name: {DefaultProfile.Quote(name)}", $"{pad}  path: {DefaultProfile.Quote(path)}"];
    }

    /// <summary>Adds the entry to <paramref name="text"/>.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="name">The repo's name.</param>
    /// <param name="path">The repo's path.</param>
    /// <returns>The new text, or null when the file's 'repos:' has a layout this edit leaves alone.</returns>
    internal static string? Add(string text, string name, string path)
    {
        var eol = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";   // the file keeps the line ending it has
        var lines = new List<string>(text.ReplaceLineEndings("\n").Split('\n'));
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);   // the final newline, put back on write
        }

        var repos = lines.FindIndex(static l => l.StartsWith("repos:", StringComparison.Ordinal));
        if (repos < 0)
        {
            lines.Add("repos:");
            lines.AddRange(Entry(name, path, 2));
            return string.Join(eol, lines) + eol;
        }

        var rest = Rest(lines[repos]);
        if (rest is not ("" or "[]"))
        {
            return null;
        }

        if (rest == "[]")
        {
            lines[repos] = "repos:";
        }

        var end = BlockEnd(lines, repos);
        lines.InsertRange(end, Entry(name, path, ItemIndent(lines, repos, end)));
        return string.Join(eol, lines) + eol;
    }

    /// <summary>How the profile's repos meet a new entry: the same one, another with its name or path, or none.</summary>
    /// <param name="json">The profile as JSON (UTF-8).</param>
    /// <param name="name">The repo's name.</param>
    /// <param name="path">The repo's path.</param>
    /// <returns>The match.</returns>
    internal static RepoMatch Match(byte[] json, string name, string path)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("repos", out var repos) || repos.ValueKind != JsonValueKind.Array)
        {
            return RepoMatch.None;
        }

        var found = RepoMatch.None;
        foreach (var entry in repos.EnumerateArray())
        {
            var meets = Meets(entry, name, path);
            if (meets == RepoMatch.Same)
            {
                return RepoMatch.Same;
            }

            found = meets == RepoMatch.Other ? RepoMatch.Other : found;
        }

        return found;
    }

    private static RepoMatch Meets(JsonElement entry, string name, string path)
    {
        var theirs = Text(entry, "name");
        var sameName = string.Equals(theirs, name, StringComparison.OrdinalIgnoreCase);
        var samePath = string.Equals(Text(entry, "path")?.TrimEnd('/'), path.TrimEnd('/'), StringComparison.Ordinal);
        if (sameName && samePath && string.Equals(theirs, name, StringComparison.Ordinal))
        {
            return RepoMatch.Same;
        }

        return sameName || samePath ? RepoMatch.Other : RepoMatch.None;
    }

    private static string? Text(JsonElement map, string key) => map.ValueKind == JsonValueKind.Object && map.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // What follows 'repos:' on its line, without a trailing comment.
    private static string Rest(string line)
    {
        var rest = line["repos:".Length..];
        var hash = rest.IndexOf(" #", StringComparison.Ordinal);
        return (hash >= 0 ? rest[..hash] : rest).Trim();
    }

    // One past the last non-blank line that belongs to the block (the lines after 'repos:' indented deeper, or '- ' lines at its own indent).
    private static int BlockEnd(List<string> lines, int repos)
    {
        var end = repos + 1;
        for (var i = repos + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0 || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (Indent(line) == 0 && !line.StartsWith("- ", StringComparison.Ordinal) && line != "-")
            {
                break;
            }

            end = i + 1;
        }

        return end;
    }

    private static int ItemIndent(List<string> lines, int repos, int end)
    {
        for (var i = repos + 1; i < end; i++)
        {
            if (lines[i].TrimStart().StartsWith("- ", StringComparison.Ordinal))
            {
                return Indent(lines[i]);
            }
        }

        return 2;
    }

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;
}
