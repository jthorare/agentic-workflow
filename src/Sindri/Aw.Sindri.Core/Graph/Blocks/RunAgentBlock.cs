using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'run_agent' block (issue 2258, PR 7): launches an agent session through <see cref="IAgentRunPort"/>, waits for its report and hands on the
/// fields the node declared plus 'status', 'branch', 'commit', 'sessionId' and 'usage'.
/// </summary>
public sealed class RunAgentBlock(IAgentRunPort agents) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "run_agent";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("repo", BlockFieldType.Text),
        new("agent", BlockFieldType.Text),
        new("prompt", BlockFieldType.Text),
        new("outputs", BlockFieldType.Map),
        new("readOnlyRepos", BlockFieldType.TextList, Required: false),
        new("timeout", BlockFieldType.Text, Required: false),
        new("connections", BlockFieldType.TextList, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"repo":"client-repo","agent":"claude-code","prompt":"Compare the contracts in api-repo with the client.","outputs":{"drifted":"boolean","prUrl":"string?"},"timeout":"45m"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        if (config["outputs"] is JsonObject declared)
        {
            foreach (var (name, type) in declared)
            {
                fields[name] = type is JsonValue value && value.TryGetValue<string>(out var text) ? text : "string";
            }
        }

        // why: the launch facts come with every run, whatever the node declares
        fields["status"] = "string";
        fields["branch"] = "string?";
        fields["commit"] = "string?";
        fields["sessionId"] = "string";
        fields["usage"] = "object?";
        return new BlockOutputs(fields);
    }

    /// <summary>Checks an agent's report against the node's declared outputs; the error (SND-GRAPH-109) names the field and what was expected, so the agent can report again.</summary>
    /// <param name="config">The node's config.</param>
    /// <param name="report">The agent's JSON report.</param>
    /// <returns>Success, or the first field that is missing or of the wrong type.</returns>
    public static Result<Unit> CheckReport(RunAgentConfig config, JsonObject report)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(report);
        foreach (var (name, declared) in config.Outputs.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            var optional = declared.EndsWith('?');
            var type = declared.TrimEnd('?');
            if (report[name] is not { } value)
            {
                if (!optional)
                {
                    return SindriErrors.GraphAgentReport.With($"the report is missing '{name}' (expected {type}); report again with every declared field");
                }

                continue;
            }

            if (!Fits(value, type))
            {
                return SindriErrors.GraphAgentReport.With($"the report's '{name}' must be {type}; report again with the right type");
            }
        }

        return Unit.Value;
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var config = RunAgentConfig.Parse(context.Config);
        if (config.IsError)
        {
            return config.Error;
        }

        var ran = await agents.Run(new AgentRunRequest(context.RunId, context.Node, config.Value), cancellationToken).ConfigureAwait(false);
        if (ran.IsError)
        {
            return ran.Error;
        }

        var report = ran.Value;
        var checkedReport = CheckReport(config.Value, report.Fields);
        if (checkedReport.IsError)
        {
            return checkedReport.Error;   // why: the complete path already refused a bad report; this guards a recovery that completed a node from .sindri-out
        }

        var outputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var name in config.Value.Outputs.Keys)
        {
            outputs[name] = report.Fields[name]?.DeepClone();
        }

        outputs["status"] = report.Status;
        outputs["branch"] = report.Branch;
        outputs["commit"] = report.Commit;
        outputs["sessionId"] = report.SessionId.Value;
        outputs["usage"] = report.Usage?.DeepClone();
        return new BlockResult(outputs);
    }

    private static bool Fits(JsonNode value, string type) => type switch
    {
        "string" => value.GetValueKind() == JsonValueKind.String,
        "number" => value.GetValueKind() == JsonValueKind.Number,
        "boolean" => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
        "object" => value is JsonObject,
        "array" => value is JsonArray,
        _ => true,
    };
}
