using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// A block that can run: the type's description (<see cref="IGraphBlockType"/>) plus what one node execution does.
/// It sees only its <see cref="BlockContext"/> and the ports it was built with, so it holds no Temporal types and runs the same in a test.
/// </summary>
public interface IGraphBlock : IGraphBlockType
{
    /// <summary>Runs one node.</summary>
    /// <param name="context">The node's interpolated config and the values it may read.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <returns>The outputs and the branches taken, or the reason the node failed.</returns>
    public ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken);
}
