using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'for_each' block: takes a list and checks it fits; the engine then runs the nodes up to the matching 'collect' once per item.</summary>
/// <param name="limits">Where the defaults for 'maxItems' and 'batchSize' come from.</param>
public sealed class ForEachBlock(IForEachLimitSource limits) : IGraphBlock
{
    /// <inheritdoc />
    public string Type => "for_each";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("items", BlockFieldType.Text), new("maxItems", BlockFieldType.Number, Required: false), new("batchSize", BlockFieldType.Number, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"items":"{{search.results}}","maxItems":100}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException("red");

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException("red: " + limits);
}
