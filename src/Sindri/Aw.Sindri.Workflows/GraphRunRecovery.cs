using Aw.Kernel;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Issue 2367, red stub: at serve start, ends every 'running' run that Temporal has no workflow for and starts the next queued run of its workflow. No logic yet.
/// </summary>
/// <param name="runs">Where runs are recorded.</param>
/// <param name="probe">Asks Temporal whether a run's workflow exists.</param>
/// <param name="mirror">Appends the events of an ended run.</param>
/// <param name="queue">Starts the next queued run of a workflow.</param>
/// <param name="logger">Where an ended run is logged.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Meziantou.Analyzer", "MA0182:Avoid unused internal types", Justification = "Red stub for issue 2367: the fix registers it with the host.")]
internal sealed class GraphRunRecovery(IWorkflowRunStore runs, IGraphRunProbe probe, GraphMirror mirror, IGraphQueueAdvance queue, ILogger<GraphRunRecovery> logger)
{
    private readonly IWorkflowRunStore _runs = runs;
    private readonly IGraphRunProbe _probe = probe;
    private readonly GraphMirror _mirror = mirror;
    private readonly IGraphQueueAdvance _queue = queue;
    private readonly ILogger<GraphRunRecovery> _logger = logger;

    /// <summary>Ends every running run Temporal has no workflow for.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many runs it ended, or the reason it could not finish.</returns>
    public ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        _ = (_runs, _probe, _mirror, _queue, _logger, cancellationToken);
        throw new NotSupportedException("GraphRunRecovery is not built yet (issue 2367).");
    }
}
