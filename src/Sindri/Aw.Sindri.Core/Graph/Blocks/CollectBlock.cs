using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'collect' block: ends a 'for_each'. In each item's run it hands on its 'value'; once the last item is done the engine writes the node's own output,
/// 'items' (the values in item order; an item whose branch skipped the collect adds nothing) and 'count'.
/// </summary>
public sealed class CollectBlock : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["items"] = "array", ["count"] = "number" });

    /// <inheritdoc />
    public string Type => "collect";

    /// <inheritdoc />
    public string Title => "Collect";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("value", BlockFieldType.Text, Caption: "Value to collect")]);

    /// <inheritdoc />
    public string Example => """{"value":"{{summarize.text}}"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["value"] = context.Config["value"]?.DeepClone() }));
    }
}
