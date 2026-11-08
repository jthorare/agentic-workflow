using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Turns a trigger into a run: starts it, queues it or skips it by the workflow's concurrency mode (a port so Features need not reference the Temporal project).</summary>
public interface IGraphRunStarter
{
    /// <summary>Starts, queues or skips one run.</summary>
    /// <param name="request">The trigger.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>What the trigger became, or SND-GRAPH-100 when the run id is used.</returns>
    public ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken);
}
