using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'run_agent' block: one agent session per node run, in a fresh worktree on its own branch, inside the sandbox. This class describes the block (config, outputs, checks); the walk
/// runs it in the workflow's agent loop (launch, a heartbeating wait, the report check, close), because the session outlives a plain activity and a person may take it over.
/// </summary>
public sealed class RunAgentBlock : IGraphBlock
{
    /// <summary>The fields every 'run_agent' node hands on besides the ones its config declares.</summary>
    public static IReadOnlyDictionary<string, string> Standard { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["status"] = "string",
        ["branch"] = "string",
        ["commit"] = "string",
        ["sessionId"] = "string?",
        ["usage"] = "object",
    };

    /// <inheritdoc />
    public string Type => "run_agent";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new(
    [
        new("repo", BlockFieldType.Text),
        new("prompt", BlockFieldType.Text),
        new("readOnlyRepos", BlockFieldType.TextList, Required: false),
        new("agent", BlockFieldType.Text, Required: false),
        new("outputs", BlockFieldType.Map, Required: false),
        new("timeout", BlockFieldType.Text, Required: false),
        new("connections", BlockFieldType.TextList, Required: false),
        new("resources", BlockFieldType.Map, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"repo":"web","prompt":"Check {{trigger.ref}} for contract drift and fix what you can","outputs":{"drifted":"boolean","summary":"string"},"timeout":"45m"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, type) in config["outputs"] as JsonObject ?? [])
        {
            if (type is JsonValue value && value.TryGetValue<string>(out var text))
            {
                fields[name] = text;
            }
        }

        foreach (var (name, type) in Standard)
        {
            fields[name] = type;
        }

        return new BlockOutputs(fields);
    }

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config) => RunAgentConfig.Problems(config);

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<BlockResult>>(SindriErrors.GraphBlockFailed.With("a run_agent node runs in the workflow's agent loop, not as a plain block"));
}
