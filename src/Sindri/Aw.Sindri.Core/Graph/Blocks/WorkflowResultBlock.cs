using System.Text.Json.Nodes;
using Aw.Kernel;

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
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = (results, context, cancellationToken);   // stub until the block is written (#2258)
        throw new NotSupportedException();
    }
}
