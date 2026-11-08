using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'condition' block: evaluates 'when' and leaves by 'true' or 'false' only.</summary>
public sealed class ConditionBlock : IGraphBlock
{
    /// <inheritdoc />
    public string Type => throw new NotSupportedException();

    /// <inheritdoc />
    public bool IsTrigger => throw new NotSupportedException();

    /// <inheritdoc />
    public BlockSchema Config => throw new NotSupportedException();

    /// <inheritdoc />
    public string Example => throw new NotSupportedException();

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
