using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'run_workflow' block: starts another workflow's active revision, with a payload, and either goes on at once or (with 'wait') until the called run ends. The call is guarded
/// by <see cref="Guards.CallDepth"/>: at most 'workflow.callDepth' deep, and never the run's own workflow.
/// </summary>
public sealed class RunWorkflowBlock(IGraphCallPort calls) : IGraphBlock
{
    /// <summary>The longest 'wait' waits when 'waitSeconds' is not set.</summary>
    public const int DefaultWaitSeconds = 600;

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["runId"] = "string",
        ["status"] = "string",
        ["outputs"] = "any?",
    });

    /// <inheritdoc />
    public string Type => "run_workflow";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("workflow", BlockFieldType.Text), new("payload", BlockFieldType.Map, Required: false), new("wait", BlockFieldType.Boolean, Required: false), new("waitSeconds", BlockFieldType.Number, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"workflow":"triage","payload":{"pr":"{{trigger.payload.pr}}"},"wait":true}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = calls;
        throw new NotSupportedException("run_workflow is not written yet");
    }
}
