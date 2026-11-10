using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'memory_search' block (picker label Recall): recalls facts from a Sindri memory by meaning and hands on their texts as 'facts' and the ranking 'mode'.</summary>
public sealed class MemorySearchBlock(IGraphMemoryPort memory) : IGraphBlock
{
    private const int MaxLimit = 50;   // why: the search handler clamps to 1..50 (SearchMemoryService); the doc says so

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
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var problems = new List<(string Field, string Problem)>();
        if (ConfigValues.Text(config["level"])?.Trim() is { Length: > 0 } level && !ConfigValues.IsReference(level) && MatchLevels.Parse(level).IsError)
        {
            problems.Add(("level", $"'{level}' is not a match level; use loose, balanced or strict"));
        }

        if (ConfigValues.Text(config["limit"])?.Trim() is { Length: > 0 } limit && !ConfigValues.IsReference(limit) && !(ConfigValues.Int(config["limit"]) is > 0))
        {
            problems.Add(("limit", $"'{limit}' is not a positive whole number; use 1 to {MaxLimit}"));
        }

        return problems;
    }

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
        var limit = Limit(context.Config);
        if (limit.IsError)
        {
            return limit.Error;
        }

        var recalled = await memory.Recall(new MemoryToRecall(name, query, string.IsNullOrEmpty(level) ? null : level, limit.Value), cancellationToken).ConfigureAwait(false);
        if (recalled.IsError)
        {
            return recalled.Error;
        }

        var facts = new JsonArray([.. recalled.Value.Facts.Select(static f => (JsonNode?)JsonValue.Create(f))]);
        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["facts"] = facts, ["mode"] = recalled.Value.Mode });
    }

    // why: a limit that is not a whole number must fail the node, not silently become the default
    private static Result<int?> Limit(JsonObject config)
    {
        if (ConfigValues.Text(config["limit"])?.Trim() is not { Length: > 0 } text)
        {
            return (int?)null;
        }

        return ConfigValues.Int(config["limit"]) is { } number
            ? number
            : SindriErrors.GraphBlockFailed.With($"memory_search's 'limit' is '{text}'; write a whole number from 1 to {MaxLimit}");
    }
}
