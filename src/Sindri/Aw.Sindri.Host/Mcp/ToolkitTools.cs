using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Features.BuildToolkit;
using Aw.Sindri.Features.ListToolkitCalls;
using Aw.Sindri.Features.ListToolkits;
using Aw.Sindri.Features.StartToolkit;
using Aw.Sindri.Features.StopToolkit;
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

    private static readonly FencePlan _calls = new("sindri:toolkit-calls", [new("calls[].input"), new("calls[].output"), new("calls[].error")]);

    private static readonly FencePlan _plain = new("sindri:toolkit", []);

    /// <summary>Runs a management tool through its Features slice: toolkit_list, toolkit_start, toolkit_stop and toolkit_calls answer at once, toolkit_build starts the job and answers its id.</summary>
    /// <param name="tool">One of the five management names.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="arguments">The call's arguments.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The tool result.</returns>
    internal static async ValueTask<CallToolResult> CallManagement(string tool, IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (tool == List)
        {
            return await ListAll(services, arguments, ct).ConfigureAwait(false);
        }

        var refused = McpArguments.Only(tool, arguments, tool == Calls ? ["name", "limit"] : ["name"]);
        var name = McpArguments.RequiredString(tool, arguments, "name", "github-triage");
        var limit = McpArguments.OptionalLong(tool, arguments, "limit", min: 1);
        if ((refused ?? name.Failed() ?? limit.Failed()) is { } error)
        {
            return McpResults.Failure(services, error);
        }

        return tool switch
        {
            Build => await BuildOne(services, name.Value, ct).ConfigureAwait(false),
            Start => await StartOne(services, name.Value, ct).ConfigureAwait(false),
            Stop => await StopOne(services, name.Value, ct).ConfigureAwait(false),
            _ => await RecentCalls(services, name.Value, limit.Value, ct).ConfigureAwait(false),
        };
    }

    private static async ValueTask<CallToolResult> ListAll(IServiceProvider services, IDictionary<string, JsonElement>? arguments, CancellationToken ct)
    {
        if (McpArguments.None(List, arguments) is { } extra)
        {
            return McpResults.Failure(services, extra);
        }

        var listed = await services.GetRequiredService<IService<ListToolkitsRequest, ListToolkitsResponse>>().Handle(new ListToolkitsRequest(), ct).ConfigureAwait(false);
        return listed.IsError
            ? McpResults.Failure(services, listed.Error)
            : Answer(new JsonObject { ["toolkits"] = new JsonArray([.. listed.Value.Toolkits.Select(Row)]) }, $"{listed.Value.Toolkits.Count.ToString(CultureInfo.InvariantCulture)} toolkits", _plain);
    }

    private static async ValueTask<CallToolResult> BuildOne(IServiceProvider services, string name, CancellationToken ct)
    {
        var started = await services.GetRequiredService<IService<BuildToolkitRequest, JobStartedResponse>>().Handle(new BuildToolkitRequest(name), ct).ConfigureAwait(false);
        return started.IsError
            ? McpResults.Failure(services, started.Error)
            : Answer(new JsonObject { ["name"] = name, ["jobId"] = started.Value.JobId.Value }, $"building {name} (job {started.Value.JobId.Value})", _plain);
    }

    private static async ValueTask<CallToolResult> StartOne(IServiceProvider services, string name, CancellationToken ct)
    {
        var up = await services.GetRequiredService<IService<StartToolkitRequest, StartToolkitResponse>>().Handle(new StartToolkitRequest(name), ct).ConfigureAwait(false);
        return up.IsError ? McpResults.Failure(services, up.Error) : Answer(State(up.Value.Name, up.Value.State), $"{up.Value.Name} is running", _plain);
    }

    private static async ValueTask<CallToolResult> StopOne(IServiceProvider services, string name, CancellationToken ct)
    {
        var down = await services.GetRequiredService<IService<StopToolkitRequest, StopToolkitResponse>>().Handle(new StopToolkitRequest(name), ct).ConfigureAwait(false);
        return down.IsError ? McpResults.Failure(services, down.Error) : Answer(State(down.Value.Name, down.Value.State), $"{down.Value.Name} stopped", _plain);
    }

    private static async ValueTask<CallToolResult> RecentCalls(IServiceProvider services, string name, long? limit, CancellationToken ct)
    {
        var request = limit is { } n ? new ListToolkitCallsRequest(name, (int)Math.Min(n, 200)) : new ListToolkitCallsRequest(name);
        var recent = await services.GetRequiredService<IService<ListToolkitCallsRequest, ListToolkitCallsResponse>>().Handle(request, ct).ConfigureAwait(false);
        return recent.IsError
            ? McpResults.Failure(services, recent.Error)
            : Answer(new JsonObject { ["calls"] = new JsonArray([.. recent.Value.Calls.Select(CallRow)]) }, $"{recent.Value.Calls.Count.ToString(CultureInfo.InvariantCulture)} calls of {name}", _calls);
    }

    private static Error? Failed<T>(this Result<T> result) => result.IsError ? result.Error : null;

    private static JsonObject State(string name, ToolkitState state) => new() { ["name"] = name, ["state"] = state.ToString().ToLowerInvariant() };

    private static JsonObject Row(Toolkit toolkit) => new()
    {
        ["name"] = toolkit.Name,
        ["state"] = toolkit.State.ToString().ToLowerInvariant(),
        ["tools"] = new JsonArray([.. (toolkit.Manifest?.Tools ?? []).Select(static t => (JsonNode)JsonValue.Create(t.Name))]),
        ["builtAt"] = toolkit.BuiltAt?.ToString("O", CultureInfo.InvariantCulture),
        ["createdBy"] = toolkit.CreatedBy,
    };

    private static JsonObject CallRow(ToolkitCall call) => new()
    {
        ["toolkit"] = call.Toolkit,
        ["tool"] = call.Tool,
        ["caller"] = call.Caller,
        ["input"] = call.Input,
        ["output"] = call.Output,
        ["truncated"] = call.Truncated,
        ["error"] = call.Error,
        ["durationMs"] = call.DurationMs,
        ["at"] = call.At.ToString("O", CultureInfo.InvariantCulture),
    };

    private static CallToolResult Answer(JsonObject body, string summary, FencePlan plan)
    {
        using var document = JsonDocument.Parse(body.ToJsonString());
        return new CallToolResult { StructuredContent = McpFencing.Apply(document.RootElement, plan), Content = [new TextContentBlock { Text = summary }] };
    }

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
