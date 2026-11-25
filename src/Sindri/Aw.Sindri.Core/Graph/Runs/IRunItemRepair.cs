using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// The repair pass of issue 2588: a 'create_item' node that made its item but could not write the run's 'item.linked' row or the run's item only warned. The pass reads the node outputs of a run
/// and writes what is missing: per item the link row, per run the item made first when the run has none. Writing it twice is a no-op, so a pass may run any number of times.
/// </summary>
public interface IRunItemRepair
{
    /// <summary>Repairs every run of every workflow (serve start).</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many writes it made, or why it stopped (no lease, the ledger or a store failed).</returns>
    public ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken);

    /// <summary>Repairs one run (when it finishes).</summary>
    /// <param name="run">The run.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many writes it made, or why it stopped.</returns>
    public ValueTask<Result<int>> RepairRun(WorkflowRunId run, CancellationToken cancellationToken);
}
