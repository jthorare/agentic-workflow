using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'memory_search' block (picker label Recall): recalls facts from a Sindri memory by meaning and hands on their texts as 'facts' and the ranking 'mode'.</summary>
public sealed class MemorySearchBlock(IGraphMemoryPort memory) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["facts"] = "array", ["mode"] = "string" });

    /// <inheritdoc />
    public string Type => "memory_search";

    /// <inheritdoc />
    public string Title => "Recall";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Data;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("memory", BlockFieldType.Text),
        new("query", BlockFieldType.Text),
        new("level", BlockFieldType.Text, Required: false),
        new("limit", BlockFieldType.Number, Required: false),
    ]);

    /// <inheritdoc />
    public string Example => """{"memory":"drift-check","query":"last checked commit and open PR","level":"balanced","limit":5}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        _ = memory;
        throw new NotSupportedException();
    }
}
