using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Issue 2367 (and the live sweep, issue 2999): ends every 'running' run that Temporal has no workflow for (serve died between the store moving the run to running and the launch) as failed
/// ('lost at restart'), appends its 'workflow.run.started' and 'workflow.run.finished', then starts the next queued run of each affected workflow. A run Temporal knows is left alone, and a
/// second pass finds nothing. When Temporal cannot be asked the pass returns the error and the host tries again.
/// </summary>
/// <param name="runs">Where runs are recorded.</param>
/// <param name="probe">Asks Temporal whether a run's workflow exists.</param>
/// <param name="mirror">Appends the events of an ended run.</param>
/// <param name="queue">Starts the next queued run of a workflow.</param>
/// <param name="logger">Where an ended run is logged.</param>
internal sealed partial class GraphRunRecovery(IWorkflowRunStore runs, IGraphRunProbe probe, GraphMirror mirror, IGraphQueueAdvance queue, ILogger<GraphRunRecovery> logger) : IGraphRunRecovery
{
    internal const string Reason = "lost at restart: serve stopped before the run's workflow started";

    private readonly IWorkflowRunStore _runs = runs;
    private readonly IGraphRunProbe _probe = probe;
    private readonly GraphMirror _mirror = mirror;
    private readonly IGraphQueueAdvance _queue = queue;
    private readonly ILogger<GraphRunRecovery> _logger = logger;

    // why: a run already Failed is no longer 'running', so a retry never meets it again; when its events could not be appended they wait here for the next pass
    private readonly List<WorkflowRun> _unrecorded = [];

    /// <summary>Ends every running run Temporal has no workflow for.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many runs it ended, or the reason it could not finish.</returns>
    public async ValueTask<Result<int>> Run(CancellationToken cancellationToken)
    {
        // why: a run this process admitted or promoted may still be launching, so it is never an orphan of the last process; the starter lists only runs from before this process
        var running = await _queue.RunningBeforeThisProcess(cancellationToken).ConfigureAwait(false);
        return await Pass(running, DateTimeOffset.MaxValue, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends every running run that started before <paramref name="startedBefore"/> and Temporal has no workflow for, runs of this process included unless their launch is still in flight; a later one may be a launch just begun.</summary>
    /// <param name="startedBefore">The cutoff: runs that started at or after it are left alone.</param>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many runs it ended, or the reason it could not finish.</returns>
    public async ValueTask<Result<int>> Run(DateTimeOffset startedBefore, CancellationToken cancellationToken)
    {
        // why: the live sweep (issue 2999) also ends runs this process started whose workflow is gone; only a launch still in flight is spared, and the cutoff keeps young runs out
        var running = await _queue.RunningNotLaunching(cancellationToken).ConfigureAwait(false);
        return await Pass(running, startedBefore, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<int>> Pass(Result<IReadOnlyList<WorkflowRun>> running, DateTimeOffset startedBefore, CancellationToken cancellationToken)
    {
        if (running.IsError)
        {
            return running.Error;
        }

        var pending = await RecordPending(cancellationToken).ConfigureAwait(false);
        var workflows = new List<WorkflowId>();
        var (ended, failure) = await EndLost(running.Value.Where(r => r.StartedAt < startedBefore), workflows, cancellationToken).ConfigureAwait(false);
        failure ??= pending;
        // why: a queue head whose hand-over failed (a busy store) has no running row to name its workflow; the store refuses to promote a workflow that has a run in flight (issue 3102)
        var queued = await _runs.QueuedWorkflows(cancellationToken).ConfigureAwait(false);
        if (queued.IsSuccess)
        {
            workflows.AddRange(queued.Value.Where(w => !workflows.Contains(w)));
        }
        // why: rows already failed are no longer 'running', so a retry never sees them; their queues advance now, even when the pass stopped on an error
        var advanced = await AdvanceQueues(workflows, cancellationToken).ConfigureAwait(false);
        return failure ?? advanced ?? (queued.IsError ? queued.Error : (Result<int>)ended);
    }

    private async ValueTask<(int Ended, Error? Failure)> EndLost(IEnumerable<WorkflowRun> candidates, List<WorkflowId> workflows, CancellationToken cancellationToken)
    {
        var ended = 0;
        Error? failure = null;
        foreach (var run in candidates)
        {
            var outcome = await EndIfLost(run, cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return (ended, outcome.Error);   // why: Temporal can't be asked or a write failed; the host retries the pass
            }

            if (outcome.Value is not { } failed)
            {
                continue;
            }

            ended++;
            if (!workflows.Contains(run.WorkflowId))
            {
                workflows.Add(run.WorkflowId);
            }

            var recorded = await Record(failed, cancellationToken).ConfigureAwait(false);
            failure ??= recorded;
        }

        return (ended, failure);
    }

    /// <summary>Appends the events of runs an earlier pass failed but could not record.</summary>
    private async ValueTask<Error?> RecordPending(CancellationToken cancellationToken)
    {
        WorkflowRun[] waiting;
        lock (_unrecorded)
        {
            waiting = [.. _unrecorded];
            _unrecorded.Clear();
        }

        Error? failure = null;
        foreach (var run in waiting)
        {
            var recorded = await Record(run, cancellationToken).ConfigureAwait(false);   // why: a failed append keeps the run waiting again
            failure ??= recorded;
        }

        return failure;
    }

    private void Keep(WorkflowRun run)
    {
        lock (_unrecorded)
        {
            _unrecorded.Add(run);
        }
    }

    private async ValueTask<Error?> AdvanceQueues(List<WorkflowId> workflows, CancellationToken cancellationToken)
    {
        foreach (var workflow in workflows)
        {
            var next = await _queue.StartNextQueued(workflow, cancellationToken).ConfigureAwait(false);
            if (next.IsError)
            {
                return next.Error;
            }
        }

        return null;
    }

    /// <summary>Fails one run when Temporal has no workflow for it.</summary>
    /// <returns>The failed row; null when Temporal knows the run or another writer ended it first; or the error.</returns>
    private async ValueTask<Result<WorkflowRun?>> EndIfLost(WorkflowRun run, CancellationToken cancellationToken)
    {
        var known = await _probe.Exists(run.Id, cancellationToken).ConfigureAwait(false);
        if (known.IsError)
        {
            return known.Error;
        }

        if (known.Value)
        {
            return (WorkflowRun?)null;
        }

        var done = await _runs.SetStatus(run.Id, WorkflowRunStatus.Failed, outputs: null, Reason, cancellationToken).ConfigureAwait(false);
        if (done.IsError)
        {
            return done.Error;
        }

        if (done.Value.Status != WorkflowRunStatus.Failed)
        {
            return (WorkflowRun?)null;
        }

        Lost(_logger, run.Id.Value, run.WorkflowId.Value);
        return done.Value;
    }

    private async ValueTask<Error?> Record(WorkflowRun failed, CancellationToken cancellationToken)
    {
        try
        {
            await _mirror.AppendStarted(failed, cancellationToken).ConfigureAwait(false);
            await _mirror.Append(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(failed.Id, GraphMirror.Name(WorkflowRunStatus.Failed), _mirror.Clean(Reason), GraphMirror.By, WorkflowRunMode.Live),
                $"{EventTypes.WorkflowRunFinished}:{failed.Id.Value}", failed.ItemId?.Value, cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (InvalidOperationException e)   // boundary: GraphMirror throws when the lease is gone or the append fails; the queue still advances, the pass reports the error
        {
            Keep(failed);
            return SindriErrors.NotRunning.With(e.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {Run} of workflow {Workflow} was left running with no Temporal workflow; ended as failed (lost at restart)")]
    private static partial void Lost(ILogger logger, string run, string workflow);
}
