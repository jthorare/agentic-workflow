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
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ConfigValues.Text(context.Config["memory"])?.Trim() is not { Length: > 0 } name)
        {
            return SindriErrors.GraphBlockFailed.With("memory_search needs a 'memory' name");
        }

        if (ConfigValues.Text(context.Config["query"])?.Trim() is not { Length: > 0 } query)
        {
            return SindriErrors.GraphBlockFailed.With("memory_search needs a non-empty 'query'");
        }

        var level = ConfigValues.Text(context.Config["level"])?.Trim();
        var recalled = await memory.Recall(new MemoryToRecall(name, query, string.IsNullOrEmpty(level) ? null : level, ConfigValues.Int(context.Config["limit"])), cancellationToken).ConfigureAwait(false);
        if (recalled.IsError)
        {
            return recalled.Error;
        }

        var facts = new JsonArray([.. recalled.Value.Facts.Select(static f => (JsonNode?)JsonValue.Create(f))]);
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["facts"] = facts, ["mode"] = recalled.Value.Mode });
    }
}
