using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>
/// The next run number of a workflow's counted runs: the manual runs ('sindri wf run', the Run button) and the re-runs from a step share one count, so neither
/// starts inside the other's numbers (#3204 review: two re-runs took 3 and 4, and the next Run tried 3 and 4 and failed SND-GRAPH-100 on every click).
/// Schedule runs number by epoch minute and connection-event and workflow-call runs by a hash, so they stay out of this count and probe past a used id themselves.
/// </summary>
public static class WorkflowRunNumbers
{
    /// <summary>The trigger kind of a manual run.</summary>
    public const string Manual = "manual";

    /// <summary>The trigger kind of a re-run from a step.</summary>
    public const string Rerun = "rerun";

    /// <summary>One more than the highest occurrence of the workflow's live manual runs and re-runs, over every revision; 1 when it has none.</summary>
    /// <param name="runs">The runs.</param>
    /// <param name="workflow">The workflow.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The next occurrence, or why it could not be read.</returns>
    public static async ValueTask<Result<int>> Next(IWorkflowRunStore runs, WorkflowId workflow, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runs);
        var manual = await runs.LatestOccurrence(workflow, Manual, cancellationToken).ConfigureAwait(false);
        if (manual.IsError)
        {
            return manual.Error;
        }

        var rerun = await runs.LatestOccurrence(workflow, Rerun, cancellationToken).ConfigureAwait(false);
        if (rerun.IsError)
        {
            return rerun.Error;
        }

        return Math.Max(manual.Value ?? 0, rerun.Value ?? 0) + 1;
    }
}
