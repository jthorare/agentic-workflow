using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'manual' trigger: a run started by hand, handing on the payload it was started with.</summary>
public sealed class ManualBlock : IGraphBlock
{
    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal) { ["payload"] = "object?" });

    /// <inheritdoc />
    public string Type => "manual";

    /// <inheritdoc />
    public bool IsTrigger => true;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([]);

    /// <inheritdoc />
    public string Example => "{}";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.FromResult<Result<BlockResult>>(new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal) { ["payload"] = context.Resolve("trigger") }));
    }
}
