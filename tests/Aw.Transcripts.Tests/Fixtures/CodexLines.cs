using System.Text.Json.Nodes;

namespace Aw.Transcripts.Tests.Fixtures;

/// <summary>Synthetic Codex rollout lines: '{ timestamp, type, payload }'.</summary>
internal static class CodexLines
{
    /// <summary>The session_meta line; <paramref name="source"/> is 'cli', 'exec', or a subagent object.</summary>
    public static string Meta(DateTimeOffset at, string id, string cwd, JsonNode? source = null) =>
        Line(at, "session_meta", new JsonObject { ["id"] = id, ["timestamp"] = Wire.Time(at), ["cwd"] = cwd, ["originator"] = "codex_cli", ["source"] = source ?? "cli", ["base_instructions"] = new JsonObject { ["text"] = "instructions" } });

    /// <summary>A subagent's source: spawned by <paramref name="parent"/>.</summary>
    public static JsonNode Spawned(string parent) => new JsonObject { ["subagent"] = new JsonObject { ["thread_spawn"] = new JsonObject { ["parent_thread_id"] = parent, ["depth"] = 1 } } };

    /// <summary>The typed prompt as Codex records it.</summary>
    public static string UserMessage(DateTimeOffset at, string text) =>
        Line(at, "event_msg", new JsonObject { ["type"] = "user_message", ["message"] = text, ["images"] = new JsonArray() });

    /// <summary>A user response item (what the model saw: AGENTS.md, environment context, the prompt).</summary>
    public static string UserItem(DateTimeOffset at, string text) =>
        Line(at, "response_item", new JsonObject { ["type"] = "message", ["role"] = "user", ["content"] = new JsonArray(new JsonObject { ["type"] = "input_text", ["text"] = text }) });

    /// <summary>An assistant message.</summary>
    public static string Said(DateTimeOffset at, string text) =>
        Line(at, "response_item", new JsonObject { ["type"] = "message", ["role"] = "assistant", ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = text }) });

    /// <summary>A tool call.</summary>
    public static string Call(DateTimeOffset at, string name) =>
        Line(at, "response_item", new JsonObject { ["type"] = "function_call", ["name"] = name, ["arguments"] = "{\"cmd\":\"secret\"}", ["call_id"] = "c1" });

    /// <summary>A tool call's output.</summary>
    public static string CallOutput(DateTimeOffset at, string output) =>
        Line(at, "response_item", new JsonObject { ["type"] = "function_call_output", ["call_id"] = "c1", ["output"] = output });

    private static string Line(DateTimeOffset at, string type, JsonObject payload) =>
        new JsonObject { ["timestamp"] = Wire.Time(at), ["type"] = type, ["payload"] = payload }.ToJsonString();
}
