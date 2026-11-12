using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'memory_write' block (picker label Remember): writes a fact to a Sindri memory, or refreshes a near duplicate, with the run's workflow, run and item as its provenance.</summary>
public sealed class MemoryWriteBlock(IGraphMemoryPort memory) : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["id"] = "string", ["action"] = "string", ["warning"] = "string?" });

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
        new("memory", BlockFieldType.Text, Caption: "Memory"),
        new("fact", BlockFieldType.Text, Caption: "Fact to remember"),
        new("tags", BlockFieldType.TextList, Required: false, Caption: "Tags"),
        new("expires", BlockFieldType.Text, Required: false, Caption: "Forget after"),
    ]);

    /// <inheritdoc />
    public string Example => """{"memory":"drift-check","fact":"Checked api-repo at {{trigger.payload.commit}}","tags":["checks"],"expires":"30d"}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ConfigValues.Text(config["expires"])?.Trim() is { Length: > 0 } expires && !ConfigValues.IsReference(expires) && BlockDuration.Parse(expires).IsError
            ? [("expires", $"'{expires}' is not a length; write one such as '30d', '12h' or '45m'")]
            : [];
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (ConfigValues.Text(context.Config["memory"])?.Trim() is not { Length: > 0 } name)
        {
            return SindriErrors.GraphBlockFailed.With("memory_write needs a 'memory' name");
        }

        if (ConfigValues.Text(context.Config["fact"])?.Trim() is not { Length: > 0 } fact)
        {
            return SindriErrors.GraphBlockFailed.With("memory_write needs a non-empty 'fact'");
        }

        TimeSpan? expiresIn = null;
        if (ConfigValues.Text(context.Config["expires"])?.Trim() is { Length: > 0 } expires)
        {
            var parsed = BlockDuration.Parse(expires);
            if (parsed.IsError)
            {
                return SindriErrors.GraphBlockFailed.With($"memory_write's 'expires' is '{expires}'; write a length such as '30d', '12h' or '45m'");
            }

            expiresIn = parsed.Value;
        }

        var written = await memory.Remember(new MemoryToRemember(context.RunId, name, fact, Tags(context.Config["tags"]), expiresIn), cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return written.Error;
        }

        var outputs = new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["id"] = written.Value.Key, ["action"] = written.Value.Action };
        if (written.Value.Warning is { } warning)
        {
            outputs["warning"] = warning;   // only when something wasn't recorded: the fact exists, so the node still succeeds
        }

        return new BlockResult(outputs);
    }

    // why: a tag may arrive as a list or, from a whole reference to text, as one string; blank entries mean nothing
    private static List<string> Tags(JsonNode? node) =>
        node switch
        {
            JsonArray list => [.. list.Select(static t => ConfigValues.Text(t)?.Trim()).OfType<string>().Where(static t => t.Length > 0)],
            null => [],
            _ => ConfigValues.Text(node)?.Trim() is { Length: > 0 } one ? [one] : [],
        };
}
