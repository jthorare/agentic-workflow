using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Toolkits over /sindri/mcp (agent-first PR 16): every running, unpaused toolkit's tools are listed as '&lt;toolkit&gt;__&lt;tool&gt;' with the manifest's schema, and a call to one goes to the runtime
/// (<see cref="IRunningToolkits"/>); the answer is untrusted text, so every string in it is fenced. The five toolkit_* management tools are listed always.
/// </summary>
internal static class ToolkitTools
{
    /// <summary>toolkit_list's name.</summary>
    internal const string List = "toolkit_list";

    /// <summary>toolkit_build's name.</summary>
    internal const string Build = "toolkit_build";

    /// <summary>toolkit_start's name.</summary>
    internal const string Start = "toolkit_start";

    /// <summary>toolkit_stop's name.</summary>
    internal const string Stop = "toolkit_stop";

    /// <summary>toolkit_calls's name.</summary>
    internal const string Calls = "toolkit_calls";

    private static readonly FencePlan _answer = new("sindri:toolkit", [new("result", EveryString: true)]);

    private static JsonObject Name => ContextTools.Property("string", "The toolkit's name, e.g. github-triage");

    /// <summary>The five management tools.</summary>
    internal static IReadOnlyList<Tool> Management { get; } =
    [
        new()
        {
            Name = List,
            Description = "List Sindri toolkits (small programs Sindri builds and runs in a sandbox) with their state and tools.",
            InputSchema = ContextTools.Schema([]),
            Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
        },
        new()
        {
            Name = Build,
            Description = "Build a toolkit (zero warnings and its tests passing) so it can start. A job; it takes a heavy slot.",
            InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name }, required: ["name"]),
            Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
        },
        new()
        {
            Name = Start,
            Description = "Start a built toolkit; its tools then appear in tools/list as <toolkit>__<tool>.",
            InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name }, required: ["name"]),
            Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
        },
        new()
        {
            Name = Stop,
            Description = "Switch a running toolkit off; its process stops and its tools leave tools/list.",
            InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name }, required: ["name"]),
            Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = false, IdempotentHint = true, OpenWorldHint = false },
        },
        new()
        {
            Name = Calls,
            Description = "The recent calls to a toolkit's tools: tool, caller, duration and error (inputs and outputs are scrubbed and capped).",
            InputSchema = ContextTools.Schema(new JsonObject { ["name"] = Name, ["limit"] = ContextTools.Property("integer", "At most this many calls (default 20)", minimum: 1) }, required: ["name"]),
            Annotations = new ToolAnnotations { ReadOnlyHint = true, OpenWorldHint = false },
        },
    ];

    /// <summary>Whether <paramref name="tool"/> is one of the management tools.</summary>
    /// <param name="tool">A tool name.</param>
    /// <returns>True for toolkit_list, build, start, stop and calls.</returns>
    internal static bool IsManagement(string tool) => Management.Any(m => string.Equals(m.Name, tool, StringComparison.Ordinal));

    /// <summary>The tools of every running toolkit; a paused, off, built or failed toolkit offers none.</summary>
    /// <param name="runtime">The toolkit runtime, or null when this build has none.</param>
    /// <returns>One tool per toolkit tool.</returns>
    internal static IReadOnlyList<Tool> Running(IRunningToolkits? runtime)
    {
        var tools = new List<Tool>();
        foreach (var toolkit in (runtime?.Toolkits() ?? []).Where(static t => t.State == ToolkitState.Running))
        {
            foreach (var tool in toolkit.Manifest?.Tools ?? [])
            {
                var name = ToolNames.For(toolkit.Name, tool.Name);
                if (name.IsSuccess)
                {
                    tools.Add(new Tool
                    {
                        Name = name.Value,
                        Description = tool.Description,
                        InputSchema = JsonDocument.Parse(tool.InputSchema).RootElement.Clone(),
                        Annotations = new ToolAnnotations { OpenWorldHint = false },
                    });
                }
            }
        }

        return tools;
    }

    /// <summary>Whether a call names a toolkit tool: the toolkit form, and not a tool Sindri lists itself.</summary>
    /// <param name="name">The called name.</param>
    /// <returns>True when it splits as '&lt;toolkit&gt;__&lt;tool&gt;'.</returns>
    internal static bool IsToolkitTool(string name) => ToolNames.TryParse(name, out _, out _);

    /// <summary>Runs a management tool. Until the runtime slices are merged it answers SND-TK-011.</summary>
    /// <param name="services">The request's services.</param>
    /// <returns>The refusal.</returns>
    internal static CallToolResult CallManagement(IServiceProvider services) =>
        McpResults.Failure(services, SindriErrors.ToolkitManagementUnavailable.With("this build has no toolkit runtime behind the toolkit_* tools yet (fix: use the sindri toolkit verbs once they ship)"));

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
            { State: ToolkitState.Paused } => SindriErrors.ToolkitPaused.With($"Toolkit '{toolkitName}' is paused (fix: sindri toolkit resume {toolkitName})"),
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
