using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Turns a trigger into a run (issue 2258, PR 6): records the run under its id (a used id is refused), applies the workflow's
/// concurrency mode, and starts the Temporal workflow through <see cref="IGraphRunLauncher"/>. A launch that fails because Temporal is unreachable leaves nothing behind
/// (a first run's row is removed, a promoted queued run goes back to queued), so a schedule's next pass or a retried finish tries again; any other launch failure ends the
/// run as failed and appends 'workflow.run.started' and 'workflow.run.finished' so the run shows in the view and the ledger.
/// </summary>
/// <param name="runs">Where runs are recorded.</param>
/// <param name="launcher">Starts the Temporal workflow.</param>
/// <param name="mirror">Appends the events of a run whose launch failed.</param>
/// <param name="clock">The clock the run's start time comes from.</param>
/// <param name="logger">Where a skipped trigger is logged.</param>
internal sealed partial class GraphRunStarter(IWorkflowRunStore runs, IGraphRunLauncher launcher, GraphMirror mirror, TimeProvider clock, ILogger<GraphRunStarter> logger) : IGraphRunStarter, IGraphQueueAdvance, IDisposable
{
    private readonly IWorkflowRunStore _runs = runs;
    private readonly IGraphRunLauncher _launcher = launcher;
    private readonly GraphMirror _mirror = mirror;
    private readonly TimeProvider _clock = clock;
    private readonly ILogger<GraphRunStarter> _logger = logger;

    // why: recovery must tell a run this process is launching from one the previous process left behind. A run is recorded here under the same gate as the store write that makes it
    // 'running', and recovery reads 'running' under that gate, so it sees a run as running only when it is already listed here. Ids are never removed: a run this process started is never
    // an orphan of the last one, a launch that is still in flight is covered the same way, and a few strings per run cost nothing against a process's life.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _startedHere = new(StringComparer.Ordinal);

    // why: a promoted queued run keeps the start time it had when it was queued, so the live sweep (issue 2999) must not take a run whose launch call has not returned for an orphan; added under the gate, removed when the launch ends
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _launching = new(StringComparer.Ordinal);

    /// <summary>Starts, queues or skips one run.</summary>
    /// <param name="request">The trigger.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>What the trigger became, or SND-GRAPH-100 when the run id is used.</returns>
    public async ValueTask<Result<GraphStart>> Start(GraphStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = WorkflowRunId.Of(request.Slug, request.Revision, request.Occurrence);
        var run = new WorkflowRun(id, request.Workflow, request.Revision, request.Trigger, WorkflowRunStatus.Running,
            Outputs: null, Error: null, ItemId: null, _clock.GetUtcNow(), EndedAt: null, request.Mode);
        // why: the in-flight check and the insert are one store call, so concurrent triggers cannot both start.
        var admitted = await Admit(run, request.Concurrency, cancellationToken).ConfigureAwait(false);
        if (admitted.IsError)
        {
            return admitted.Error;
        }

        if (admitted.Value == GraphStartKind.Skipped)
        {
            Skipped(_logger, id.Value, request.Slug.Value);
        }

        if (admitted.Value != GraphStartKind.Started)
        {
            return new GraphStart(admitted.Value, id);
        }

        var launched = await Launch(run, fromQueue: false, cancellationToken).ConfigureAwait(false);
        if (launched.IsError)
        {
            // why: a failed run is Failed now, so a run queued behind it must get its turn; when Temporal was unreachable the row is gone and nothing ran. The caller still sees the launch error.
            if (!Unreachable(launched.Error))
            {
                _ = await StartNextQueued(request.Workflow, cancellationToken).ConfigureAwait(false);
            }

            return launched.Error;
        }

        return new GraphStart(GraphStartKind.Started, id);
    }

    /// <inheritdoc />
    public async ValueTask<Result<WorkflowRunId?>> StartNextQueued(WorkflowId workflow, CancellationToken cancellationToken)
    {
        while (true)
        {
            // why: one store call checks that nothing of the workflow is in flight and starts the oldest queued run, so two runs ending together cannot both promote one
            var promoted = await Promote(workflow, cancellationToken).ConfigureAwait(false);
            if (promoted.IsError)
            {
                return promoted.Error;
            }

            if (promoted.Value is not { } run)
            {
                return (WorkflowRunId?)null;
            }

            // why: a run whose start fails is marked failed by Launch and the next queued run gets the turn; when Temporal is unreachable every other launch would fail the same way,
            // so the loop stops, the run is back in the queue, and the caller (the finishing run's activity) is retried
            var launched = await Launch(run, fromQueue: true, cancellationToken).ConfigureAwait(false);
            if (launched.IsSuccess)
            {
                return (WorkflowRunId?)run.Id;
            }

            if (Unreachable(launched.Error))
            {
                return launched.Error;
            }
        }
    }

    /// <summary>Releases the gate.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowRun>>> RunningBeforeThisProcess(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var running = await _runs.Running(cancellationToken).ConfigureAwait(false);
            return running.Map(list => (IReadOnlyList<WorkflowRun>)[.. list.Where(r => !_startedHere.Contains(r.Id.Value))]);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<Result<IReadOnlyList<WorkflowRun>>> RunningNotLaunching(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var running = await _runs.Running(cancellationToken).ConfigureAwait(false);
            return running.Map(list => (IReadOnlyList<WorkflowRun>)[.. list.Where(r => !_launching.ContainsKey(r.Id.Value))]);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>The store's admit, with the run recorded as started here before the write can be seen and forgotten again when it did not become a running run.</summary>
    private async ValueTask<Result<GraphStartKind>> Admit(WorkflowRun run, GraphConcurrency mode, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var added = _startedHere.Add(run.Id.Value);
            var admitted = await _runs.Admit(run, mode, cancellationToken).ConfigureAwait(false);
            if (added && (admitted.IsError || admitted.Value != GraphStartKind.Started))
            {
                _ = _startedHere.Remove(run.Id.Value);
            }

            if (admitted.IsSuccess && admitted.Value == GraphStartKind.Started)
            {
                _launching[run.Id.Value] = 0;
            }

            return admitted;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>The store's promote, with the promoted run recorded as started here under the same gate as the write.</summary>
    private async ValueTask<Result<WorkflowRun?>> Promote(WorkflowId workflow, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var promoted = await _runs.PromoteOldestQueued(workflow, cancellationToken).ConfigureAwait(false);
            if (promoted.IsSuccess && promoted.Value is { } run)
            {
                _ = _startedHere.Add(run.Id.Value);
                _launching[run.Id.Value] = 0;
            }

            return promoted;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    internal static bool Unreachable(Error error) =>
        string.Equals(error.Code, SindriErrors.EngineUnavailable.Code, StringComparison.Ordinal) || string.Equals(error.Code, SindriErrors.NotRunning.Code, StringComparison.Ordinal);

    private async ValueTask<Result<bool>> Launch(WorkflowRun run, bool fromQueue, CancellationToken cancellationToken)
    {
        try
        {
            return await LaunchCore(run, fromQueue, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _launching.TryRemove(run.Id.Value, out _);
        }
    }

    private async ValueTask<Result<bool>> LaunchCore(WorkflowRun run, bool fromQueue, CancellationToken cancellationToken)
    {
        var launched = await _launcher.Launch(run, cancellationToken).ConfigureAwait(false);
        if (launched.IsSuccess)
        {
            return launched;
        }

        if (Unreachable(launched.Error))
        {
            // why: nothing ran. A queued run goes back to queued; a first run's row is removed so the same run id can be tried again (a Failed row would make the next try SND-GRAPH-100).
            if (fromQueue)
            {
                _ = await _runs.SetStatus(run.Id, WorkflowRunStatus.Queued, outputs: null, error: null, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _ = await _runs.Discard(run.Id, cancellationToken).ConfigureAwait(false);
            }

            return launched;
        }

        // why: a run that never started must not stay in flight and block 'one-per-workflow' for good, and it must show in the view and the ledger.
        var ended = await _runs.SetStatus(run.Id, WorkflowRunStatus.Failed, outputs: null, launched.Error.Message, cancellationToken).ConfigureAwait(false);
        if (ended.IsSuccess && ended.Value.Status == WorkflowRunStatus.Failed)
        {
            try
            {
                await _mirror.AppendStarted(ended.Value, cancellationToken).ConfigureAwait(false);
                await _mirror.Append(EventTypes.WorkflowRunFinished, new WorkflowRunFinishedPayload(run.Id, GraphMirror.Name(WorkflowRunStatus.Failed), _mirror.Clean(launched.Error.Message), GraphMirror.By, WorkflowRunMode.Live),
                    $"{EventTypes.WorkflowRunFinished}:{run.Id.Value}", ended.Value.ItemId?.Value, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException e)   // boundary: GraphMirror throws when the lease is gone or the append fails; the run is already Failed, so say so in the log
            {
                LedgerMissed(_logger, run.Id.Value, e.Message);
            }
        }

        return launched;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Run {Run} failed to launch and its events could not be appended: {Reason}")]
    private static partial void LedgerMissed(ILogger logger, string run, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Run {Run} of workflow {Slug} skipped: another run is in flight and the workflow runs one at a time")]
    private static partial void Skipped(ILogger logger, string run, string slug);
}
