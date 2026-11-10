using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'memory_forget' block (picker label Forget): forgets one fact of a Sindri memory by id, or every fact with a tag, and hands on how many it forgot as 'count'.</summary>
public sealed class MemoryForgetBlock(IGraphMemoryPort memory) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["count"] = "number" });

    /// <inheritdoc />
    public string Type => "memory_forget";

    /// <inheritdoc />
    public string Title => "Forget";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("memory", BlockFieldType.Text),
        new("fact", BlockFieldType.Text, Required: false),
        new("tag", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"memory":"drift-check","tag":"stale"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = memory;
        throw new NotSupportedException();
    }
}
