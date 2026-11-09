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

    /// <summary>
    /// Runs after <see cref="Execute"/> succeeded and the node's outputs are known: for a block that must remember something only when its node worked ('only_new' records the keys it passed on).
    /// A failure here fails the node. The call can repeat for one node, so a block makes it safe to run twice. Nothing by default.
    /// </summary>
    /// <param name="context">The node's context, as <see cref="Execute"/> got it.</param>
    /// <param name="result">What <see cref="Execute"/> returned.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the node failed after all.</returns>
    public ValueTask<Result<Unit>> Commit(BlockContext context, BlockResult result, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
