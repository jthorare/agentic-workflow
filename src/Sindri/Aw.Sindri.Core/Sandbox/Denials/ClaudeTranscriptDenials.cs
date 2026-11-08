using System.Text.Json;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Reads denials out of a Claude Code transcript tail (sandbox spec §3.6, §15 "Transcript formats for denial reading").</summary>
public static partial class ClaudeTranscriptDenials
{
    [GeneratedRegex(@"^Permission to use (?<tool>\S+)(?: with command (?<cmd>.*?))? has been denied", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Permission { get; }

    /// <summary>Reads the lines of a transcript.</summary>
    /// <param name="jsonlLines">The last lines of the session's jsonl; bad lines are skipped.</param>
    /// <returns>The policy denials and the paths tool calls failed on.</returns>
    public static TranscriptDenials Read(IEnumerable<string> jsonlLines)
    {
        ArgumentNullException.ThrowIfNull(jsonlLines);
        List<DenialObservation> observations = [];
        List<string> paths = [];
        Dictionary<string, string> toolNames = new(StringComparer.Ordinal);
        var seen = 0;
        foreach (var line in jsonlLines)
        {
            if (++seen > TranscriptText.MaxLines)
            {
                break;
            }

            using var doc = TranscriptText.Parse(line);
            if (doc is not null && TranscriptText.Obj(doc.RootElement, "message") is { } message
                && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    ReadBlock(block, toolNames, observations, paths);
                }
            }
        }

        return new TranscriptDenials(observations, paths);
    }

    private static void ReadBlock(JsonElement block, Dictionary<string, string> toolNames, List<DenialObservation> observations, List<string> paths)
    {
        var type = TranscriptText.Str(block, "type");
        if (type == "tool_use" && TranscriptText.Str(block, "id") is { } id && TranscriptText.Str(block, "name") is { } name && toolNames.Count < TranscriptText.MaxLines)
        {
            toolNames[id] = name;
        }
        else if (type == "tool_result")
        {
            var text = ResultText(block);
            var tool = TranscriptText.Str(block, "tool_use_id") is { } useId && toolNames.TryGetValue(useId, out var n) ? n : null;
            if (PolicyDenial(text, tool) is { } denial)
            {
                Add(observations, denial);
            }
            else if (text.Length > 0)
            {
                TranscriptText.AddPaths(text, paths);
            }
        }
    }

    private static DenialObservation? PolicyDenial(string text, string? tool)
    {
        var m = Permission.Match(text);
        if (m.Success)
        {
            return new DenialObservation(DenialKind.Policy, PolicyTarget(m.Groups["tool"].Value, m.Groups["cmd"].Value), DenialRules.InnerPolicy);
        }

        return text.Contains("denied by your permission settings", StringComparison.Ordinal) || text.Contains("running in don't ask mode", StringComparison.Ordinal)
            ? new DenialObservation(DenialKind.Policy, tool ?? "Tool", DenialRules.InnerPolicy)
            : null;
    }

    private static void Add(List<DenialObservation> observations, DenialObservation o)
    {
        if (observations.Count < TranscriptText.MaxObservations)
        {
            observations.Add(o);
        }
    }

    /// <summary>The tool, plus for a shell command its first word and a following subcommand word: "Bash: git push", never the arguments.</summary>
    private static string PolicyTarget(string tool, string command)
    {
        var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return tool;
        }

        var head = words[0];
        if (words.Length > 1 && words[1].Length <= 24 && words[1].All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') && words[1][0] != '-')
        {
            head = $"{head} {words[1]}";
        }

        return head.Length > 48 ? $"{tool}: {head[..48]}" : $"{tool}: {head}";
    }

    private static string ResultText(JsonElement block)
    {
        if (!block.TryGetProperty("content", out var c))
        {
            return string.Empty;
        }

        if (c.ValueKind == JsonValueKind.String)
        {
            return c.GetString() ?? string.Empty;
        }

        if (c.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        List<string> parts = [];
        foreach (var item in c.EnumerateArray())
        {
            if (TranscriptText.Str(item, "text") is { } t)
            {
                parts.Add(t);
            }
        }

        return string.Join('\n', parts);
    }
}
