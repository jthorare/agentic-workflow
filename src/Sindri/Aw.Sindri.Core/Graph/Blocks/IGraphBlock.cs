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
    /// Runs once the whole run has succeeded, for each node of this block that succeeded in it: for a block that must remember something only when the run worked ('only_new' records the keys it passed on).
    /// A run that failed or was cancelled never calls it. A failure here fails the run. The call can repeat for one node, so a block makes it safe to run twice. Nothing by default.
    /// </summary>
    /// <param name="context">The node's context, rebuilt from the stored outputs of the nodes before it, as <see cref="Execute"/> got it.</param>
    /// <param name="result">What <see cref="Execute"/> returned, read back from the node's stored output.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or why the run failed after all.</returns>
    public ValueTask<Result<Unit>> Commit(BlockContext context, BlockResult result, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Unit.Value);
}
