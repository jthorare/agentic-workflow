using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'manual' trigger: a run started by hand, handing on the payload it was started with.</summary>
public sealed class ManualBlock : IGraphBlock
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
