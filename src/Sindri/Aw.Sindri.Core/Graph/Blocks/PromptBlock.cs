using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'prompt' block: asks the model and checks the answer against the 'output' JSON schema; a bad answer fails the node with the reason.</summary>
public sealed class PromptBlock(IGraphModelPort model) : IGraphBlock
{
    /// <summary>The port this block writes through.</summary>
    public IGraphModelPort Model { get; } = model;

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
