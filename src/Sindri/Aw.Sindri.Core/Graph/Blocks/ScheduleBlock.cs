using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'schedule' trigger: a cron and a timezone; hands on the tick it fired for.</summary>
public sealed class ScheduleBlock : IGraphBlock
{
    /// <inheritdoc />
    public string Type => throw new NotSupportedException();

    /// <inheritdoc />
    public bool IsTrigger => throw new NotSupportedException();

    /// <inheritdoc />
    public BlockSchema Config => throw new NotSupportedException();

    /// <inheritdoc />
    public string Example => throw new NotSupportedException();

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => throw new NotSupportedException();

    /// <inheritdoc />
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
