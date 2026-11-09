using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'wait' block: says when the node wakes; the workflow sleeps on a Temporal timer for that long, so nothing polls.</summary>
/// <param name="clock">The clock; the system clock when null.</param>
public sealed class WaitBlock(TimeProvider? clock = null) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "wait";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([]);

    /// <inheritdoc />
    public string Example => "{}";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException($"{clock}");
}
