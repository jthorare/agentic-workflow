using System.Text;
using System.Text.Json;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The text-level helpers the profile writers share: they edit the live profile's lines, not the parsed document, so every other key and comment stays as it was.</summary>
internal static class YamlProfileLines
{
    // The first line under `parent` (before the next line at its own indent or less) that is `key` one level in.
    internal static int Child(List<string> lines, int parent, string key)
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


    internal static int Indent(string line) => line.Length - line.TrimStart(' ').Length;

    internal static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    internal static JsonElement? Descend(JsonElement from, params string[] path)
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


    internal static void Write(string path, string text)
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

    // 'profile set': the same write, except the file keeps the mode it has (the owner may have loosened or tightened it) instead of getting the owner-only default.
    internal static void WriteKeepingMode(string path, string text)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));
        }

        File.Move(temporary, path, overwrite: true);
    }
}
