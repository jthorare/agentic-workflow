using System.Text.Json.Nodes;

namespace Aw.Transcripts.Tests.Fixtures;

/// <summary>Synthetic Cursor agent-transcript lines (config/hooks/tests/fixtures/cursor/transcript.jsonl and the old scorer's notes).</summary>
internal static class CursorLines
{
    /// <summary>A user line; the prompt wrapped the way Cursor wraps it.</summary>
    public static string User(string query, string? timestamp = null)
    {
        var text = (timestamp is null ? "" : $"<timestamp>{timestamp}</timestamp>\n") + $"<user_query>\n{query}\n</user_query>";
        return new JsonObject { ["role"] = "user", ["message"] = new JsonObject { ["content"] = Wire.Texts(text) } }.ToJsonString();
    }

    /// <summary>A user line with raw text (no user_query wrapper).</summary>
    public static string UserRaw(string text) => new JsonObject { ["role"] = "user", ["message"] = new JsonObject { ["content"] = Wire.Texts(text) } }.ToJsonString();

    /// <summary>An assistant line.</summary>
    public static string Assistant(string? text, params string[] tools)
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

        return new JsonObject { ["role"] = "assistant", ["message"] = new JsonObject { ["content"] = content } }.ToJsonString();
    }

    /// <summary>The end-of-turn marker.</summary>
    public static string TurnEnded() => new JsonObject { ["type"] = "turn_ended", ["status"] = "success" }.ToJsonString();
}
