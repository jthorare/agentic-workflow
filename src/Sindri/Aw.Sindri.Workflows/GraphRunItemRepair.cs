using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Runs the run-item repair pass (issue 2588, <see cref="IRunItemRepair"/>) for a run that just ended. The pass is best effort here: an error, or an exception, is logged as a warning and never
/// fails the caller, so a ledger blip cannot make the run's finish retry. The next finish and the next serve start repair it again.
/// </summary>
/// <param name="repair">The pass; null when the host has none (a test rig), which does nothing.</param>
/// <param name="logger">Where a failed pass is logged.</param>
internal sealed partial class GraphRunItemRepair(IRunItemRepair? repair, ILogger<GraphRunItemRepair> logger)
{
    /// <summary>Repairs the run's item links; never throws, except to cancel.</summary>
    /// <param name="run">The run that ended.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>Completes when the pass ended or failed.</returns>
    public async Task AfterFinish(WorkflowRunId run, CancellationToken cancellationToken)
    {
        if (repair is null)
        {
            return;
        }

        try
        {
            var repaired = await repair.RepairRun(run, cancellationToken).ConfigureAwait(false);
            if (repaired.IsError)
            {
                Failed(logger, run.Value, repaired.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)   // boundary: a broken repair must not fail or retry the run's finish
        {
            Failed(logger, run.Value, e.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The item repair after run {Run} failed: {Detail}; the next finish or serve start repairs it")]
    private static partial void Failed(ILogger logger, string run, string detail);
}
