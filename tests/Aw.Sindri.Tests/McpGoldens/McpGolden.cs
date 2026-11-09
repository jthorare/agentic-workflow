using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Tests.ApiGoldens;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.McpGoldens;

/// <summary>Records a whole tool result (isError, structuredContent, the text blocks) under Golden/Mcp, normalized as the API goldens are (D6).</summary>
internal static class McpGolden
{
    /// <summary>Compares <paramref name="result"/> with Golden/Mcp/&lt;tool&gt;/&lt;scenario&gt;.json.</summary>
    /// <param name="result">The tool result.</param>
    /// <param name="home">The host's temp home, normalized to '&lt;home&gt;'.</param>
    /// <param name="tool">The tool's name.</param>
    /// <param name="scenario">The scenario's name.</param>
    public static void Match(CallToolResult result, string home, string tool, string scenario)
    {
        ArgumentNullException.ThrowIfNull(result);
        var json = new JsonObject
        {
            ["isError"] = result.IsError == true,
            ["structuredContent"] = result.StructuredContent is { } structured ? JsonNode.Parse(structured.GetRawText()) : null,
            ["content"] = new JsonArray([.. result.Content.OfType<TextContentBlock>().Select(b => (JsonNode?)JsonValue.Create(b.Text))]),
        };
        // why: a filed request's ids and sequence depend on how many ids the projector pump drew while the call waited
        NormalizationRule[] filed =
        [
            new("command", "\"commandId\": \"[0-9a-f-]+\"", "\"commandId\": \"<command>\""),
            new("sequence", "\"sequence\": [0-9]+", "\"sequence\": \"<n>\""),
            new("request", "REQ-[0-9a-f]{32}", "REQ-<id>"),
            new("decision", "DEC-[0-9a-f]{32}", "DEC-<id>"),
        ];
        // why: a todo's id, the command that wrote it and where the views stand depend on how many ids and events the pump drew while the call waited
        NormalizationRule[] todo =
        [
            new("command", "\"commandId\": \"[0-9a-f-]+\"", "\"commandId\": \"<command>\""),
            new("sequence", "\"sequence\": [0-9]+", "\"sequence\": \"<n>\""),
            new("cursor", "\"cursor\": [0-9]+", "\"cursor\": \"<n>\""),
            new("ledgerHead", "\"ledgerHead\": [0-9]+", "\"ledgerHead\": \"<n>\""),
            new("todo", "td_[0-9a-z]{26}", "td_<id>"),
        ];
        Golden.Match(
            ApiGolden.Reindent(json.ToJsonString()),
            typeof(McpGolden).Assembly,
            $"Golden/Mcp/{tool}/{scenario}.json",
            new Normalization([
                new NormalizationRule("home", System.Text.RegularExpressions.Regex.Escape(home), "<home>"),
                new NormalizationRule("port", "port [0-9]+", "port <port>"),
                new NormalizationRule("build", "sindri [^,\"]+, port", "sindri <build>, port"),
                .. string.Equals(tool, "sindri_request", StringComparison.Ordinal) ? filed : [],
                .. tool.StartsWith("sindri_todo", StringComparison.Ordinal) ? todo : [],
            ]));
    }
}
