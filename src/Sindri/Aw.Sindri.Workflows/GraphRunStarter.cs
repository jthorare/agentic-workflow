using Aw.Kernel;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

// why: the host registers it with the Temporal launcher in the next commit; until then only tests build it.
#pragma warning disable MA0182
/// <summary>
/// Turns a trigger into a run (issue 2258, PR 6): records the run under its id (a used id is refused), applies the workflow's
/// concurrency mode, and starts the Temporal workflow through <see cref="IGraphRunLauncher"/>.
/// </summary>
/// <param name="runs">Where runs are recorded.</param>
/// <param name="launcher">Starts the Temporal workflow.</param>
/// <param name="clock">The clock the run's start time comes from.</param>
/// <param name="logger">Where a skipped trigger is logged.</param>
internal sealed class GraphRunStarter(IWorkflowRunStore runs, IGraphRunLauncher launcher, TimeProvider clock, ILogger<GraphRunStarter> logger)
{
    private readonly IWorkflowRunStore _runs = runs;
    private readonly IGraphRunLauncher _launcher = launcher;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<GraphRunStarter> _logger = logger;

    /// <summary>Starts, queues or skips one run.</summary>
    /// <param name="request">The trigger.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>What the trigger became, or SND-GRAPH-100 when the run id is used.</returns>
    public ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
    {
        _ = (request, cancellationToken, _runs, _launcher, _clock, _logger);
        throw new NotSupportedException();
    }
}
#pragma warning restore MA0182
