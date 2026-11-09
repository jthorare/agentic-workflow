using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'workflow_call' trigger: what a workflow declares to be started by another workflow's 'run_workflow'. It hands on the payload, the caller's run and the call depth.</summary>
public sealed class WorkflowCallTrigger : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["payload"] = "object?",
        ["parentRun"] = "string",
        ["callDepth"] = "number",
        ["by"] = "string",
    });

    /// <inheritdoc />
    public string Type => "workflow_call";

    /// <inheritdoc />
    public bool IsTrigger => true;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([]);

    /// <inheritdoc />
    public string Example => "{}";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("workflow_call is not written yet");
}
