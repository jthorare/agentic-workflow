using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Toolkit;

/// <summary>MCP tool results: the response as structured content with one short text block, or an error result carrying the AW-TOOLKIT code.</summary>
internal static class ToolResults
{
    public static CallToolResult Success(string tool, JsonElement response) => new()
    {
        StructuredContent = response,
        Content = [new TextContentBlock { Text = $"{tool} done." }],
    };

    public static CallToolResult Failure(ToolkitError definition, string message)
    {
        var node = new JsonObject { ["code"] = definition.Code, ["message"] = message };
        using var document = JsonDocument.Parse(node.ToJsonString());
        return new CallToolResult
        {
            IsError = true,
            StructuredContent = document.RootElement.Clone(),
            Content = [new TextContentBlock { Text = $"{definition.Code} {message}" }],
        };
    }

    public static JsonElement Element(JsonObject node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }
}
