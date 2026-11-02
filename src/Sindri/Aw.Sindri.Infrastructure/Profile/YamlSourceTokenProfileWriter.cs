using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// ISourceTokenProfileWriter over the live profile file (owned-index spec §5.4). It edits the text, so every other key and comment stays as it was: an existing pointer line under the source's block is replaced,
/// otherwise one is added as the block's first line. The source's block must already exist in block style (its repos are the owner's to write); a flow-style entry is a layout Sindri won't edit. The result is parsed again before it
/// is written, and the file is written atomically.
/// </summary>
internal sealed class YamlSourceTokenProfileWriter(SindriSettings settings) : ISourceTokenProfileWriter
{
    /// <inheritdoc />
    public Result<bool> SetToken(string source, string secretPointer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(secretPointer);
        return Apply(source, "token:", $"token: {YamlProfileLines.Quote(secretPointer)}", yaml => Holds(yaml, source, secretPointer));
    }

    // Reads the live profile, sets one `key: value` line under the source's block, checks the result parses back to the intended value and writes it.
    private Result<bool> Apply(string source, string key, string line, Func<string, bool> holds)
    {
        var path = settings.ProfilePath;
        string before;
        try
        {
            before = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message}");
        }

        var edited = Edit(before, source, key, line);
        if (edited.IsError)
        {
            return edited.Error;
        }

        if (string.Equals(before, edited.Value, StringComparison.Ordinal))
        {
            return false;
        }

        return holds(edited.Value)
            ? Save(path, edited.Value)
            : SindriErrors.ProfileInvalid.With($"{path} has a layout Sindri won't edit (fix: add `{line}` under context.sources.{source} by hand)");
    }

    /// <inheritdoc />
    public Result<bool> SetTeams(string source, IReadOnlyList<string> teams)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(teams);
        var line = $"teams: [{string.Join(", ", teams.Select(YamlProfileLines.Quote))}]";
        return Apply(source, "teams:", line, yaml => HoldsTeams(yaml, source, teams));
    }

    private static Result<bool> Save(string path, string text)
    {
        try
        {
            YamlProfileLines.Write(path, text);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be written: {e.Message}");
        }
    }

    private static Result<string> Edit(string text, string source, string key, string line)
    {
        var lines = new List<string>(text.ReplaceLineEndings("\n").Split('\n'));
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var context = lines.FindIndex(static l => l.TrimEnd() == "context:");
        var sources = context < 0 ? -1 : YamlProfileLines.Child(lines, context, "sources:");
        var block = sources < 0 ? -1 : YamlProfileLines.Child(lines, sources, source + ":");
        if (block < 0)
        {
            return SindriErrors.IndexNotInProfile.With($"the profile has no context.sources.{source} entry (fix: list its repos under context.sources.{source}, then connect)");
        }

        if (!string.Equals(lines[block].Trim(), source + ":", StringComparison.Ordinal))
        {
            return SindriErrors.ProfileInvalid.With($"context.sources.{source} is written in flow style, which Sindri won't edit (fix: write it as a block, or add `{line}` to it by hand)");
        }

        var indent = YamlProfileLines.Indent(lines[block]) + 2;
        var existing = YamlProfileLines.Child(lines, block, key);
        if (existing >= 0)
        {
            lines[existing] = new string(' ', YamlProfileLines.Indent(lines[existing])) + line;
        }
        else
        {
            lines.Insert(block + 1, new string(' ', indent) + line);
        }

        return string.Join('\n', lines) + "\n";
    }

    private static bool HoldsTeams(string yaml, string source, IReadOnlyList<string> teams)
    {
        if (YamlToJson.TryConvert(yaml, out var json) is not null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        return YamlProfileLines.Descend(document.RootElement, "context", "sources", source, "teams") is { ValueKind: JsonValueKind.Array } entry
            && entry.EnumerateArray().Select(static e => e.GetString()).SequenceEqual(teams, StringComparer.Ordinal);
    }

    private static bool Holds(string yaml, string source, string pointer)
    {
        if (YamlToJson.TryConvert(yaml, out var json) is not null)
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        return YamlProfileLines.Descend(document.RootElement, "context", "sources", source, "token") is { ValueKind: JsonValueKind.String } entry
            && string.Equals(entry.GetString(), pointer, StringComparison.Ordinal);
    }
}
