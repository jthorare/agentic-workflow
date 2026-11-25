using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Transcripts.Tests.Fixtures;

/// <summary>Helpers the synthetic line builders share.</summary>
internal static class Wire
{
    public static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static JsonArray Texts(params string[] texts) => [.. texts.Select(t => (JsonNode)new JsonObject { ["type"] = "text", ["text"] = t })];

    public static JsonObject ToolUse(string name) => new() { ["type"] = "tool_use", ["id"] = $"toolu_{name}", ["name"] = name, ["input"] = new JsonObject { ["secret_input"] = "never read" } };
}
