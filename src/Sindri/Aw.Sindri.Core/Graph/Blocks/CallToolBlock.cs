using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'call_tool' block (picker label "Toolkit tool"): calls a running toolkit's tool ('tool' is '&lt;toolkit&gt;__&lt;tool&gt;'), or, with 'connection' set, a tool of that MCP connection or an action of that
/// rest connection's preset. The tool's JSON answer is the node's 'result'. The validator checks a toolkit tool's name and arguments against the manifest's input schema.
/// </summary>
public sealed class CallToolBlock(IRunningToolkits toolkits, IConnectionToolPort connections) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["result"] = "any" });

    /// <inheritdoc />
    public string Type => "call_tool";

    /// <inheritdoc />
    public string Title => "Toolkit tool";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Data;   // why: like 'http', a tool call may read or write; Data is where the picker puts the blocks that reach outside Sindri for an answer

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("tool", BlockFieldType.Text),
        new("connection", BlockFieldType.Text, Required: false),
        new("arguments", BlockFieldType.Map, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"tool":"notes__add_note","arguments":{"text":"{{triage.summary}}"}}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var tool = ConfigValues.Text(config["tool"]);
        if (tool is null || !string.IsNullOrWhiteSpace(ConfigValues.Text(config["connection"])))
        {
            return [];   // why: a connection's tools are listed over the network, so only a run can check them
        }

        if (!ToolNames.TryParse(tool, out var toolkitName, out var toolName))
        {
            return [("tool", $"'{tool}' is not a toolkit tool; use '<toolkit>__<tool>' such as 'notes__add_note', or set 'connection' for a connection's tool")];
        }

        var known = toolkits.Toolkits().FirstOrDefault(t => string.Equals(t.Name, toolkitName, StringComparison.Ordinal));
        if (known?.Manifest is null)
        {
            return [];   // why: the toolkit may be off while the workflow is written; a run says SND-TK-009 or SND-TK-001
        }

        var manifestTool = known.Manifest.Tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal));
        if (manifestTool is null)
        {
            return [("tool", $"toolkit '{toolkitName}' has no tool '{toolName}'; its tools are {string.Join(", ", known.Manifest.Tools.Select(static t => t.Name))}")];
        }

        return [.. SchemaCheck.Problems(manifestTool.InputSchema, config["arguments"] as JsonObject, allowReferences: true).Select(static p => ("arguments", p))];
    }

    /// <summary>Lists the tools of every running toolkit, named as a node's 'tool' field spells them.</summary>
    /// <returns>The picker rows.</returns>
    public IReadOnlyList<CallableTool> ListToolkitTools()
    {
        var rows = new List<CallableTool>();
        foreach (var toolkit in toolkits.Toolkits().Where(static t => t.State == ToolkitState.Running && t.Manifest is not null))
        {
            foreach (var tool in toolkit.Manifest?.Tools ?? [])
            {
                var name = ToolNames.For(toolkit.Name, tool.Name);
                if (name.IsSuccess)
                {
                    rows.Add(new CallableTool(Tool: name.Value, Connection: null, Description: tool.Description, Source: "toolkit"));
                }
            }
        }

        return rows;
    }

    /// <summary>Lists a connection's tools (an MCP server's tools or a rest preset's actions) for the picker.</summary>
    /// <param name="connection">The connection's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The picker rows, or why the connection can't be read.</returns>
    public async ValueTask<Result<IReadOnlyList<CallableTool>>> ListConnectionTools(string connection, CancellationToken cancellationToken)
    {
        var listed = await connections.ListTools(connection, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        IReadOnlyList<CallableTool> rows = [.. listed.Value.Select(t => new CallableTool(t.Name, connection, t.Description, t.Source))];
        return Results.Success(rows);
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tool = ConfigValues.Text(context.Config["tool"]);
        if (string.IsNullOrWhiteSpace(tool))
        {
            return SindriErrors.GraphBlockFailed.With("call_tool needs a non-empty 'tool'");
        }

        var arguments = context.Config["arguments"] as JsonObject ?? [];
        var connection = ConfigValues.Text(context.Config["connection"]);
        var answer = string.IsNullOrWhiteSpace(connection)
            ? await CallToolkit(context, tool, arguments, cancellationToken).ConfigureAwait(false)
            : await connections.Call(connection, tool, arguments.ToJsonString(), cancellationToken).ConfigureAwait(false);
        return answer.IsError
            ? answer.Error
            : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["result"] = Parse(answer.Value) });
    }

    private async ValueTask<Result<string>> CallToolkit(BlockContext context, string name, JsonObject arguments, CancellationToken cancellationToken)
    {
        if (!ToolNames.TryParse(name, out var toolkitName, out var toolName))
        {
            return SindriErrors.GraphBlockFailed.With($"'{name}' is not a toolkit tool; use '<toolkit>__<tool>', or set 'connection' for a connection's tool");
        }

        var toolkit = toolkits.Toolkits().FirstOrDefault(t => string.Equals(t.Name, toolkitName, StringComparison.Ordinal));
        if (toolkit is null)
        {
            return SindriErrors.ToolkitNotFound.With($"No toolkit named '{toolkitName}' (fix: sindri toolkit list)");
        }

        if (toolkit.State == ToolkitState.Paused)
        {
            return SindriErrors.ToolkitPaused.With($"Toolkit '{toolkitName}' is paused (fix: toolkit_resume)");
        }

        if (toolkit.State != ToolkitState.Running || toolkit.Manifest is null)
        {
            return SindriErrors.ToolkitNotBuilt.With($"Toolkit '{toolkitName}' isn't running (it is {toolkit.State.ToString().ToLowerInvariant()}; fix: sindri toolkit start {toolkitName})");
        }

        var manifestTool = toolkit.Manifest.Tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.Ordinal));
        if (manifestTool is null)
        {
            return SindriErrors.ToolkitToolUnknown.With($"Toolkit '{toolkitName}' has no tool '{toolName}'");
        }

        if (SchemaCheck.Problems(manifestTool.InputSchema, arguments, allowReferences: false) is { Count: > 0 } problems)
        {
            return SindriErrors.GraphBlockFailed.With($"the arguments don't fit {name}: {string.Join("; ", problems)}");
        }

        var caller = $"workflow:{context.RunId.Value}/{context.Node.Value}";
        return await toolkits.CallTool(toolkitName, toolName, arguments.ToJsonString(), caller, cancellationToken).ConfigureAwait(false);
    }

    // why: a tool that answers in plain words still gives the next node something to read
    private static JsonNode? Parse(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)   // boundary: the tool's answer is not JSON
        {
            return JsonValue.Create(text);
        }
    }
}
