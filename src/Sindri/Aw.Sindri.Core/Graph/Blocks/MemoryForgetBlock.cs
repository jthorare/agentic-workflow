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
        new("memory", BlockFieldType.Text, Caption: "Memory"),
        new("fact", BlockFieldType.Text, Required: false, Caption: "Fact to forget"),
        new("tag", BlockFieldType.Text, Required: false, Caption: "Forget everything tagged"),
    ]);

    /// <inheritdoc />
    public string Example => """{"memory":"drift-check","tag":"stale"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var fact = ConfigValues.Text(config["fact"])?.Trim();
        var tag = ConfigValues.Text(config["tag"])?.Trim();
        // why: a reference may come out empty or full only at run time, so only literals are judged
        if ((fact is not null && ConfigValues.IsReference(fact)) || (tag is not null && ConfigValues.IsReference(tag)))
        {
            return [];
        }

        if (fact is { Length: 0 })
        {
            return [("fact", "is empty; give a fact id, or leave 'fact' out and give a 'tag'")];
        }

        return (fact is not null) == (tag is { Length: > 0 })
            ? [(fact is not null ? "fact" : "tag", "give exactly one of 'fact' (a fact id) and 'tag'")]
            : [];
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ConfigValues.Text(context.Config["memory"])?.Trim() is not { Length: > 0 } name)
        {
            return SindriErrors.GraphBlockFailed.With("memory_forget needs a 'memory' name");
        }

        var fact = ConfigValues.Text(context.Config["fact"])?.Trim();
        if (fact is { Length: 0 })
        {
            // why: an explicit empty fact (a reference that came out empty) must not turn into "forget the whole tag"
            return SindriErrors.GraphBlockFailed.With("memory_forget's 'fact' is empty; give a fact id, or leave 'fact' out and give a 'tag'");
        }

        var tag = ConfigValues.Text(context.Config["tag"])?.Trim();
        var hasFact = !string.IsNullOrEmpty(fact);
        var hasTag = !string.IsNullOrEmpty(tag);
        if (hasFact == hasTag)
        {
            return SindriErrors.GraphBlockFailed.With("memory_forget needs exactly one of 'fact' (a fact id) and 'tag'");
        }

        var forgotten = await memory.Forget(new MemoryToForget(name, hasFact ? fact : null, hasTag ? tag : null), cancellationToken).ConfigureAwait(false);
        return forgotten.IsError
            ? forgotten.Error
            : new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["count"] = forgotten.Value });
    }
}
