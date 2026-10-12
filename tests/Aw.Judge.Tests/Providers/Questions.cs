using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Tests.Providers;

/// <summary>QuestionRefs for the provider tests.</summary>
internal static class Questions
{
    public static TimeSpan Budget { get; } = TimeSpan.FromSeconds(5);

    /// <summary>wake-gate's shape: send, batch, drop over message-meta.</summary>
    public static QuestionRef Text { get; } = new()
    {
        Name = "wake-gate",
        Outputs = ["send", "batch", "drop"],
        Prompt = "Classify the message.",
        ContentClass = ContentClass.MessageMeta,
        Criteria = new Dictionary<string, string>(StringComparer.Ordinal) { ["send"] = "worth waking for" },
    };

    /// <summary>ui-element-repair's shape: an integer extra.</summary>
    public static QuestionRef WithIndex { get; } = Text with
    {
        Name = "ui-element-repair",
        Outputs = ["chosen", "no-good-candidate"],
        ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal) { ["chosenIndex"] = new() { ["type"] = "integer" } },
    };

    /// <summary>visual-critique's shape: an image question with a string-array extra.</summary>
    public static QuestionRef Image { get; } = new()
    {
        Name = "visual-critique",
        Outputs = ["ok", "broken"],
        Prompt = "Look at the screenshot after.png.",
        ContentClass = ContentClass.Image,
        ExtraProperties = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            ["reasons"] = new() { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } },
        },
    };

    public static JsonNode Input { get; } = new JsonObject { ["text"] = "hello", ["senderKind"] = "human" };

    public static JsonObject Evidence(string directory, string? baseline = null) => new()
    {
        ["afterScreenshot"] = "after.png",
        ["baselineScreenshot"] = baseline,
        ["evidenceDir"] = directory,
    };

    /// <summary>A claude --output-format json envelope around a structured answer.</summary>
    public static string ClaudeStructured(string answerJson) =>
        $$"""{"type":"result","is_error":false,"result":"","structured_output":{{answerJson}}}""";

    /// <summary>A claude or cursor envelope whose result text is the given string.</summary>
    public static string ResultText(string text) =>
        new JsonObject { ["type"] = "result", ["is_error"] = false, ["result"] = text }.ToJsonString();
}
