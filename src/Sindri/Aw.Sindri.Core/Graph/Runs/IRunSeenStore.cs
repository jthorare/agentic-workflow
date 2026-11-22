using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Which failed runs the owner has seen (home spec §3.3, E1; 'workflow_run_seen' in ledger.db): a failed live run nobody has seen is a 'run-failed' row on Home.</summary>
public interface IRunSeenStore
{
    /// <summary>The live runs, over every workflow, that ended failed and are not marked seen, the latest end first (then the run id, descending). A test run never counts.</summary>
    /// <param name="limit">How many runs to keep, 1 or more.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The runs; none when every failed run was seen.</returns>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> FailedUnseen(int limit, CancellationToken cancellationToken);

    /// <summary>Marks a run seen. Marking it twice, or marking a run the store does not hold, changes nothing and succeeds.</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the reason the write failed.</returns>
    public ValueTask<Result<Unit>> MarkSeen(WorkflowRunId run, CancellationToken cancellationToken);
}
