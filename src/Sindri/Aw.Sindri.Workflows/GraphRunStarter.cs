using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

// why: the host registers it with the Temporal launcher in the GraphWorkflow piece (IGraphRunLauncher has no implementation yet); until then only tests build it.
#pragma warning disable MA0182
/// <summary>
/// Turns a trigger into a run (issue 2258, PR 6): records the run under its id (a used id is refused), applies the workflow's
/// concurrency mode, and starts the Temporal workflow through <see cref="IGraphRunLauncher"/>.
/// </summary>
/// <param name="runs">Where runs are recorded.</param>
/// <param name="launcher">Starts the Temporal workflow.</param>
/// <param name="clock">The clock the run's start time comes from.</param>
/// <param name="logger">Where a skipped trigger is logged.</param>
internal sealed partial class GraphRunStarter(IWorkflowRunStore runs, IGraphRunLauncher launcher, TimeProvider clock, ILogger<GraphRunStarter> logger) : IGraphRunStarter
{
    private readonly IWorkflowRunStore _runs = runs;
    private readonly IGraphRunLauncher _launcher = launcher;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<GraphRunStarter> _logger = logger;

    /// <summary>Starts, queues or skips one run.</summary>
    /// <param name="request">The trigger.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>What the trigger became, or SND-GRAPH-100 when the run id is used.</returns>
    public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
        var busy = false;
        if (request.Concurrency != GraphConcurrency.Unlimited)
        {
            var inFlight = await _runs.InFlight(request.Workflow, cancellationToken).ConfigureAwait(false);
            if (inFlight.IsError)
            {
                return inFlight.Error;
            }

            // why: the same id in flight is a re-delivered trigger, which the insert below refuses; it is not "another run".
            busy = inFlight.Value.Any(r => r.Id != id);
        }

        if (busy && request.Concurrency == GraphConcurrency.OnePerWorkflow)
        {
            Skipped(_logger, id.Value, request.Slug.Value);
            return new GraphStart(GraphStartKind.Skipped, id);
        }

        var queued = busy;
        var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, queued ? WorkflowRunStatus.Queued : WorkflowRunStatus.Running,
            Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null);
        var inserted = await _runs.Insert(run, cancellationToken).ConfigureAwait(false);
        if (inserted.IsError)
        {
            return inserted.Error;
        }

        if (queued)
        {
            return new GraphStart(GraphStartKind.Queued, id);
        }

        var launched = await Launch(run, cancellationToken).ConfigureAwait(false);
        return launched.IsError ? launched.Error : new GraphStart(GraphStartKind.Started, id);
    }

    private async ValueTask<Result<bool>> Launch(WorkflowRun run, CancellationToken cancellationToken)
    {
        var launched = await _launcher.Launch(run, cancellationToken).ConfigureAwait(false);
        if (launched.IsError)
        {
            // why: a run that never started must not stay in flight and block 'one-per-workflow' for good.
            _ = await _runs.SetStatus(run.Id, WorkflowRunStatus.Failed, outputs: null, launched.Error.Message, cancellationToken).ConfigureAwait(false);
        }

        return launched;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {Run} of workflow {Slug} skipped: another run is in flight and the workflow runs one at a time")]
    private static partial void Skipped(ILogger logger, string run, string slug);
}
#pragma warning restore MA0182
