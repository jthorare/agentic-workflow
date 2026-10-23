using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// IOnyxProfileWriter over the live profile file (spec §4.1 step 9). It edits the text, not the parsed document, so every other key and comment stays as it was: an existing `onyx:` entry is replaced, otherwise the
/// entry is added under `context.sources`, under `context`, or at the end of the file; a missing file is created (`schemaVersion: 1`, `user`). The result is parsed again before it is written; when it doesn't hold the section,
/// nothing is written and the error shows the lines to add by hand. The file is written atomically.
/// </summary>
internal sealed class YamlOnyxProfileWriter(SindriSettings settings) : IOnyxProfileWriter
{
    /// <inheritdoc />
    public Result<bool> SetOnyx(string url, string token, string version)
    {
        // The managed stack is on 127.0.0.1 and Sindri's own; an Onyx at any other address is an attached one (`onyx attach`, pod mode): network: cluster, not managed.
        var attached = !Uri.TryCreate(url, UriKind.Absolute, out var parsed) || !parsed.IsLoopback;
        var entry = $"onyx: {{ url: {Quote(url)}, token: {Quote(token)}, version: {Quote(version)}, {(attached ? "managed: false, network: cluster" : "managed: true")} }}";
        var path = settings.ProfilePath;
        string? before;
        try
        {
            before = File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message}");
        }

        var after = before is null ? NewProfile(entry) : Edit(before, entry);
        if (string.Equals(before, after, StringComparison.Ordinal))
        {
            return false;
        }

        if (!Holds(after, url, token, version))
        {
            return SindriErrors.ProfileInvalid.With($"{path} has a layout Sindri won't edit (fix: add this under context.sources by hand, then run `sindri onyx up` again): {entry}");
        }

        try
        {
            Write(path, after);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be written: {e.Message}");
        }

        return true;
    }

    private static string NewProfile(string entry) => $"schemaVersion: 1\nuser: {Environment.UserName}\ncontext:\n  sources:\n    {entry}\n";

    private static string Edit(string text, string entry)
    {
        var lines = new List<string>(text.ReplaceLineEndings("\n").Split('\n'));
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);   // the final newline, put back on write
        }

        var context = lines.FindIndex(static l => l.TrimEnd() == "context:");
        var sources = context < 0 ? -1 : Child(lines, context, "sources:");
        var onyx = sources < 0 ? -1 : Child(lines, sources, "onyx:");
        if (onyx >= 0)
        {
            var indent = Indent(lines[onyx]);
            var end = BlockEnd(lines, onyx);
            lines.RemoveRange(onyx, end - onyx);
            lines.Insert(onyx, new string(' ', indent) + entry);
        }
        else if (sources >= 0)
        {
            lines.Insert(sources + 1, new string(' ', Indent(lines[sources]) + 2) + entry);
        }
        else if (context >= 0)
        {
            lines.InsertRange(context + 1, ["  sources:", "    " + entry]);
        }
        else
        {
            lines.AddRange(["context:", "  sources:", "    " + entry]);
        }

        return string.Join('\n', lines) + "\n";
    }

    // The first line under `parent` (before the next line at its own indent or less) that is `key` one level in.
    private static int Child(List<string> lines, int parent, string key)
    {
        var parentIndent = Indent(lines[parent]);
        for (var i = parent + 1; i < lines.Count; i++)
        {
            if (lines[i].Trim().Length == 0 || lines[i].TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (Indent(lines[i]) <= parentIndent)
            {
                return -1;
            }

            if (lines[i].TrimStart().StartsWith(key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // One past the last line of the entry that starts at `start`: the lines indented deeper than it (a block-style entry), comments inside it included.
    private static int BlockEnd(List<string> lines, int start)
    {
        var indent = Indent(lines[start]);
        var end = start + 1;
        while (end < lines.Count && (lines[end].Trim().Length == 0 || Indent(lines[end]) > indent))
        {
            end++;
        }

        while (end > start + 1 && lines[end - 1].Trim().Length == 0)
        {
            end--;   // blank lines after the entry belong to what follows
        }

        return end;
    }

    private static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static bool Holds(string yaml, string url, string token, string version)
    {
        if (YamlToJson.TryConvert(yaml, out var json) is not null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        var onyx = Descend(document.RootElement, "context", "sources", "onyx");
        return onyx is { } entry && Text(entry, "url") == url && Text(entry, "token") == token && Text(entry, "version") == version;
    }

    private static JsonElement? Descend(JsonElement from, params string[] path)
    {
        var at = from;
        foreach (var key in path)
        {
            if (at.ValueKind != JsonValueKind.Object || !at.TryGetProperty(key, out at))
            {
                return null;
            }
        }

        return at;
    }

    private static string? Text(JsonElement map, string key) => map.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static void Write(string path, string text)
    {
        if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
        {
            OwnerOnly.EnsureDirectory(directory);
        }

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        OwnerOnly.RestrictFiles(temporary);
        File.Move(temporary, path, overwrite: true);
    }
}
