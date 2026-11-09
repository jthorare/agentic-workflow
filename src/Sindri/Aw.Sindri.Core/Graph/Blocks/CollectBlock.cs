using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'collect' block: ends a 'for_each'. Each item's run gives its 'value'; after the last item the node's own output is 'items', every value in item order.</summary>
public sealed class CollectBlock : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "collect";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("value", BlockFieldType.Text)]);

    /// <inheritdoc />
    public string Example => """{"value":"{{summarize.text}}"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException("red");

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException("red");
}
