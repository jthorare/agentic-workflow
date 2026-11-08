using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The 'notify' block: writes a Needs-you row through <see cref="INotifyPort"/>.</summary>
public sealed class NotifyBlock(INotifyPort notify) : IGraphBlock
{
    /// <summary>The port this block writes through.</summary>
    public INotifyPort Notify { get; } = notify;

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
