using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'memory_write' block (picker label Remember): writes a fact to a Sindri memory, or refreshes a near duplicate, with the run's workflow, run and item as its provenance.</summary>
public sealed class MemoryWriteBlock(IGraphMemoryPort memory) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = "string", ["action"] = "string" });

    /// <inheritdoc />
    public string Type => "memory_write";

    /// <inheritdoc />
    public string Title => "Remember";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Logic;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("memory", BlockFieldType.Text),
        new("fact", BlockFieldType.Text),
        new("tags", BlockFieldType.TextList, Required: false),
        new("expires", BlockFieldType.Text, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"memory":"drift-check","fact":"Checked api-repo at {{agent.checkedCommit}}","tags":["checks"],"expires":"30d"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = memory;
        throw new NotSupportedException();
    }
}
