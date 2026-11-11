using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>The call of a toolkit's own tool through the runtime, and why a call is refused.</summary>
internal static partial class ToolkitTools
{
    /// <summary>Calls a toolkit tool through the runtime.</summary>
    /// <param name="services">The request's services.</param>
    /// <param name="name">The called name, '&lt;toolkit&gt;__&lt;tool&gt;'.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The fenced answer, or the runtime's error.</returns>
    internal static async ValueTask<CallToolResult> Call(IServiceProvider services, string name, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        _ = ToolNames.TryParse(name, out var toolkitName, out var toolName);
        var runtime = services.GetService<IRunningToolkits>();
        if (Refusal(runtime, toolkitName, toolName) is { } refused)
        {
            return McpResults.Failure(services, refused);
        }

        var json = new JsonObject();
        foreach (var (key, value) in arguments ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
        {
            json[key] = JsonNode.Parse(value.GetRawText());
        }

        var answer = await runtime!.CallTool(toolkitName, toolName, json.ToJsonString(), "mcp-session", ct).ConfigureAwait(false);   // why: Refusal returned null, so a runtime exists
        if (answer.IsError)
        {
            return McpResults.Failure(services, answer.Error);
        }

        var body = new JsonObject { ["toolkit"] = toolkitName, ["tool"] = toolName, ["result"] = Parse(answer.Value) };
        using var document = JsonDocument.Parse(body.ToJsonString());
        return new CallToolResult
        {
            StructuredContent = McpFencing.Apply(document.RootElement, _answer),
            Content = [new TextContentBlock { Text = $"{name} answered" }],
        };
    }

    private static Error? Refusal(IRunningToolkits? runtime, string toolkitName, string toolName)
    {
        var toolkit = runtime?.Toolkits().FirstOrDefault(t => string.Equals(t.Name, toolkitName, StringComparison.Ordinal));
        return toolkit switch
        {
            null => SindriErrors.ToolkitNotFound.With($"No toolkit named '{toolkitName}' (fix: toolkit_list names them)"),
            { State: ToolkitState.Paused } => SindriErrors.ToolkitPaused.With($"Toolkit '{toolkitName}' is paused (fix: toolkit_resume)"),
            { State: not ToolkitState.Running } => SindriErrors.ToolkitNotBuilt.With($"Toolkit '{toolkitName}' isn't running (fix: toolkit_start)"),
            _ when toolkit.Manifest is null || !toolkit.Manifest.Tools.Any(t => string.Equals(t.Name, toolName, StringComparison.Ordinal)) =>
                SindriErrors.ToolkitToolUnknown.With($"Toolkit '{toolkitName}' has no tool '{toolName}' (fix: tools/list names them)"),
            _ => null,
        };
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: a tool may answer in plain words
        {
            return JsonValue.Create(text);
        }
    }
}
