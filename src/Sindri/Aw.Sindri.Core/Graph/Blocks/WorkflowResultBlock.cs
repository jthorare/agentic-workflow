using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'workflow_result' block: reads what a finished run of a workflow gave (its 'outputs', 'runId', 'finishedAt' and 'status') and starts nothing. 'run' is 'latest-succeeded' (the default)
/// or a run id; a run that is not finished or not that workflow's, or a workflow with no succeeded run, is SND-GRAPH-124.
/// </summary>
public sealed class WorkflowResultBlock(IGraphResultPort results) : IGraphBlock
{
    /// <summary>The 'run' value that picks the workflow's newest succeeded run.</summary>
    public const string LatestSucceeded = "latest-succeeded";

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["runId"] = "string",
        ["status"] = "string",
        ["finishedAt"] = "string?",
        ["outputs"] = "any?",
    });

    /// <inheritdoc />
    public string Type => "workflow_result";

    /// <inheritdoc />
    public string Title => "Workflow result";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Data;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("workflow", BlockFieldType.Text),
        new("run", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"workflow":"triage","run":"latest-succeeded"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = ConfigValues.Text(context.Config["workflow"]) ?? string.Empty;
        if (!WorkflowSlug.TryFrom(text, out var workflow))
        {
            return SindriErrors.GraphBlockFailed.With($"'{text}' is not a workflow slug (lowercase letters, digits and '-'; fix: use the name 'sindri workflow list' shows)");
        }

        WorkflowRunId? run = null;
        var which = ConfigValues.Text(context.Config["run"]);
        if (!string.IsNullOrWhiteSpace(which) && !string.Equals(which, LatestSucceeded, StringComparison.Ordinal))
        {
            if (!WorkflowRunId.TryFrom(which, out var id))
            {
                return SindriErrors.GraphBlockFailed.With($"'{which}' is not '{LatestSucceeded}' or a run id like 'triage@1:7'");
            }

            run = id;
        }

        var read = await results.Read(workflow, run, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var result = read.Value;
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["runId"] = result.Run.Value,
            ["status"] = result.Status,
            ["finishedAt"] = result.FinishedAt?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["outputs"] = result.Outputs?.DeepClone(),
        });
    }
}
