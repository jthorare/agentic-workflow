using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>Where a 'run_workflow' node starts the other workflow and, with 'wait', learns how it ended.</summary>
public interface IGraphCallPort
{
    /// <summary>How many calls deep a run may be: the profile's 'workflow.callDepth', default 3.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The deepest call that is allowed.</returns>
    public ValueTask<int> MaxDepth(CancellationToken cancellationToken);

    /// <summary>Starts the callee's active revision. The same call twice (one caller run, node and index) is one run.</summary>
    /// <param name="call">What to start.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The run and whether it started, queued or was skipped; or why it could not start.</returns>
    public ValueTask<Result<GraphCalled>> Start(GraphCall call, CancellationToken cancellationToken);

    /// <summary>Waits until a called run ends.</summary>
    /// <param name="run">The called run.</param>
    /// <param name="timeout">The longest to wait.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>How it ended, or why the wait gave up.</returns>
    public ValueTask<Result<GraphCallEnd>> Wait(WorkflowRunId run, TimeSpan timeout, CancellationToken cancellationToken);
}
