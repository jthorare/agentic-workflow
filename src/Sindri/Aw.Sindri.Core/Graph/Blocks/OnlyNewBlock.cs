using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'only_new' block: passes on the items of a list that this workflow has not passed on before, keyed by a field of each item. It records the keys only after the node
/// succeeded (<see cref="Commit"/>), so a node that fails leaves its items unseen and they come back on the next run.
/// </summary>
public sealed class OnlyNewBlock(IGraphSeenStore seen) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["items"] = "array", ["count"] = "number", ["skipped"] = "number" });

    /// <inheritdoc />
    public string Type => "only_new";

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([new("items", BlockFieldType.Text), new("key", BlockFieldType.Text, Required: false)]);

    /// <inheritdoc />
    public string Example => """{"items":"{{search.results}}","key":"id"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = seen;
        throw new NotSupportedException("only_new is not written yet");
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Commit(BlockContext context, BlockResult result, CancellationToken cancellationToken) =>
        throw new NotSupportedException("only_new is not written yet");
}
