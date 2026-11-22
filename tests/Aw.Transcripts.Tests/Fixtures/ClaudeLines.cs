using System.Text.Json.Nodes;
using Aw.Transcripts.Testing;

namespace Aw.Transcripts.Tests.Fixtures;

/// <summary>Synthetic Claude Code transcript lines, in the shape the real files have (no real text in them).</summary>
internal static class ClaudeLines
{
    /// <summary>A bookkeeping line: no timestamp, no message.</summary>
    public static string Bookkeeping() => new JsonObject { ["type"] = "permission-mode", ["permissionMode"] = "default", ["sessionId"] = "s" }.ToJsonString();

    /// <summary>A user line with string content; <paramref name="with"/> adds flags such as isMeta or origin.</summary>
    public static string User(string text, DateTimeOffset at, Action<JsonObject>? with = null, string cwd = StandardCorpus.Repo) =>
        Line("user", at, cwd, new JsonObject { ["role"] = "user", ["content"] = text }, with);

    /// <summary>A user line with text blocks.</summary>
    public static string UserBlocks(DateTimeOffset at, params string[] texts) =>
        Line("user", at, StandardCorpus.Repo, new JsonObject { ["role"] = "user", ["content"] = Wire.Texts(texts) }, with: null);

    /// <summary>A tool result line.</summary>
    public static string ToolResult(DateTimeOffset at, string output) =>
        Line("user", at, StandardCorpus.Repo, new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "toolu_x", ["content"] = output }),
        }, with: null);

    /// <summary>An assistant line with optional text and tool calls.</summary>
    public static string Assistant(DateTimeOffset at, string? text, params string[] tools)
    {
        var content = new JsonArray();
        if (text is not null)
        {
            content.Add(new JsonObject { ["type"] = "text", ["text"] = text });
        }

        foreach (var tool in tools)
        {
            content.Add(Wire.ToolUse(tool));
        }

        return Line("assistant", at, StandardCorpus.Repo, new JsonObject { ["role"] = "assistant", ["model"] = "claude-test", ["content"] = content }, with: null);
    }

    /// <summary>A subagent's task prompt: a sidechain user line.</summary>
    public static string Task(string text, DateTimeOffset at) => User(text, at, l => l["isSidechain"] = true);

    private static string Line(string type, DateTimeOffset at, string cwd, JsonObject message, Action<JsonObject>? with)
    {
        var line = new JsonObject
        {
            ["type"] = type,
            ["timestamp"] = Wire.Time(at),
            ["cwd"] = cwd,
            ["sessionId"] = "s",
            ["uuid"] = $"u-{Wire.Time(at)}",
            ["isSidechain"] = false,
            ["userType"] = "external",
            ["entrypoint"] = "cli",
            ["message"] = message,
        };
        with?.Invoke(line);
        return line.ToJsonString();
    }
}
