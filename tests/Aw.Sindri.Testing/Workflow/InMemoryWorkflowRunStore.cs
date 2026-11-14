using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IWorkflowRunStore"/>. Passes <see cref="WorkflowRunStoreContract"/>.</summary>
/// <param name="clock">The clock the end times come from.</param>
public sealed class InMemoryWorkflowRunStore(TimeProvider clock) : IWorkflowRunStore
{
    private readonly Lock _gate = new();
    private readonly List<WorkflowRun> _runs = [];
    private readonly Dictionary<WorkflowId, DateTimeOffset> _ticks = [];
    private readonly Dictionary<(WorkflowRunId Run, NodeId Node, int Item), NodeRun> _nodeRuns = [];

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (WorkflowRun.TriggerProblem(run.Trigger) is { } badTrigger)
        {
            return Done<WorkflowRun>(SindriErrors.GraphInvalid.With(badTrigger));
        }

        lock (_gate)
        {
            if (_runs.Exists(r => r.Id == run.Id))
            {
                return Done<WorkflowRun>(SindriErrors.GraphRunIdTaken.With($"the run '{run.Id.Value}' already exists; a trigger that fires twice is one run"));
            }

            _runs.Add(run);
            return Done<WorkflowRun>(run);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<GraphStartKind>> Admit(WorkflowRun run, GraphConcurrency mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (WorkflowRun.TriggerProblem(run.Trigger) is { } badTrigger)
        {
            return Done<GraphStartKind>(SindriErrors.GraphInvalid.With(badTrigger));
        }

        lock (_gate)
        {
            if (_runs.Exists(r => r.Id == run.Id))
            {
                return Done<GraphStartKind>(SindriErrors.GraphRunIdTaken.With($"the run '{run.Id.Value}' already exists; a trigger that fires twice is one run"));
            }

            var busy = mode != GraphConcurrency.Unlimited && run.Mode != WorkflowRunMode.Test && _runs.Exists(r => IsLiveInFlight(r, run.WorkflowId));
            if (busy && mode == GraphConcurrency.OnePerWorkflow)
            {
                return Done<GraphStartKind>(GraphStartKind.Skipped);
            }

            _runs.Add(run with { Status = busy ? WorkflowRunStatus.Queued : WorkflowRunStatus.Running });
            return Done<GraphStartKind>(busy ? GraphStartKind.Queued : GraphStartKind.Started);
        }
    }

    private static bool IsLiveInFlight(WorkflowRun run, WorkflowId workflow) =>
        run.WorkflowId == workflow && run.Mode == WorkflowRunMode.Live && run.Status is WorkflowRunStatus.Queued or WorkflowRunStatus.Running or WorkflowRunStatus.Waiting;

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun?>> PromoteOldestQueued(WorkflowId workflow, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var inFlight = _runs.Where(r => IsLiveInFlight(r, workflow)).ToList();
            if (inFlight.Count == 0 || inFlight.Exists(static r => r.Status != WorkflowRunStatus.Queued))
            {
                return Ok<WorkflowRun?>(value: null);
            }

            var oldest = inFlight.OrderBy(static r => r.StartedAt).ThenBy(static r => r.Id.Value, StringComparer.Ordinal).First();
            var at = _runs.FindIndex(r => r.Id == oldest.Id);
            _runs[at] = oldest with { Status = WorkflowRunStatus.Running };
            return Ok<WorkflowRun?>(_runs[at]);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowId>>> QueuedWorkflows(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowId> queued = [.. _runs.Where(static r => r.Status == WorkflowRunStatus.Queued).Select(static r => r.WorkflowId).Distinct().OrderBy(static w => w.Value, StringComparer.Ordinal)];
            return Ok(queued);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> Get(WorkflowRunId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var run = _runs.Find(r => r.Id == id);
            return Done<WorkflowRun>(run is null ? NotFound(id) : run);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> InFlight(WorkflowId workflow, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowRun> inFlight = [.. _runs
                .Where(r => r.WorkflowId == workflow && r.Status is WorkflowRunStatus.Queued or WorkflowRunStatus.Running or WorkflowRunStatus.Waiting)
                .OrderBy(static r => r.StartedAt)
                .ThenBy(static r => r.Id.Value, StringComparer.Ordinal)];
            return Ok(inFlight);
        }
    }

    /// <summary>Gets or sets the most rows <see cref="Recent"/> returns per call, as a store that clamps the page size would (null = no clamp).</summary>
    public int? RecentPageClamp { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Recent(WorkflowId workflow, int offset, int limit, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowRun> recent = [.. _runs
                .Where(r => r.WorkflowId == workflow)
                .OrderByDescending(static r => r.StartedAt)
                .ThenByDescending(static r => r.Id.Occurrence)
                .Skip(Math.Max(offset, 0))
                .Take(Math.Min(Math.Max(limit, 1), RecentPageClamp ?? int.MaxValue))];
            return Ok(recent);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Running(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<WorkflowRun> running = [.. _runs
                .Where(static r => r.Status == WorkflowRunStatus.Running)
                .OrderBy(static r => r.StartedAt)
                .ThenBy(static r => r.Id.Value, StringComparer.Ordinal)];
            return Ok(running);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var matching = _runs.Where(r => r.WorkflowId == workflow && r.Mode == WorkflowRunMode.Live && KindOf(r.Trigger) == triggerKind).ToList();
            int? latest = matching.Count == 0 ? null : matching.Max(static r => r.Id.Occurrence);
            return Ok(latest);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> MarkTick(WorkflowId workflow, DateTimeOffset tick, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_ticks.TryGetValue(workflow, out var last) || tick > last)
            {
                _ticks[workflow] = tick;
            }

            return Ok(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<DateTimeOffset?>> LastTick(WorkflowId workflow, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok<DateTimeOffset?>(_ticks.TryGetValue(workflow, out var last) ? last : null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken) =>
        Move(id, status, outputs, error, from => status == WorkflowRunStatus.Queued ? from == WorkflowRunStatus.Running : !IsFinal(from));

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> SetItem(WorkflowRunId id, ItemId item, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var at = _runs.FindIndex(r => r.Id == id);
            if (at < 0)
            {
                return Done<WorkflowRun>(NotFound(id));
            }

            _runs[at] = _runs[at] with { ItemId = _runs[at].ItemId ?? item };
            return Done<WorkflowRun>(_runs[at]);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> CancelQueued(WorkflowRunId id, string? error, CancellationToken cancellationToken) =>
        Move(id, WorkflowRunStatus.Cancelled, outputs: null, error, static from => from == WorkflowRunStatus.Queued);

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Discard(WorkflowRunId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Ok(_runs.RemoveAll(r => r.Id == id && r.Status == WorkflowRunStatus.Running) > 0);
        }
    }

    private ValueTask<Result<WorkflowRun>> Move(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, Func<WorkflowRunStatus, bool> allowedFrom)
    {
        lock (_gate)
        {
            var at = _runs.FindIndex(r => r.Id == id);
            if (at < 0)
            {
                return Done<WorkflowRun>(NotFound(id));
            }

            if (allowedFrom(_runs[at].Status))
            {
                _runs[at] = _runs[at] with { Status = status, Outputs = outputs ?? _runs[at].Outputs, Error = error, EndedAt = IsFinal(status) ? clock.GetUtcNow() : null };
            }

            return Done<WorkflowRun>(_runs[at]);
        }
    }

    private static bool IsFinal(WorkflowRunStatus status) => status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled;

    /// <inheritdoc/>
    public ValueTask<Result<NodeRun>> RecordNodeRun(NodeRun nodeRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(nodeRun);
        var input = NodeRunLimits.Cap(nodeRun.Input, out var inputCut);
        var output = NodeRunLimits.Cap(nodeRun.Output, out var outputCut);
        var stored = nodeRun with { Input = input, Output = output, Truncated = nodeRun.Truncated || inputCut || outputCut };
        lock (_gate)
        {
            if (!_runs.Exists(r => r.Id == stored.RunId))
            {
                return Done<NodeRun>(NotFound(stored.RunId));
            }

            _nodeRuns[(stored.RunId, stored.NodeId, stored.ItemIndex)] = stored;
            return Done<NodeRun>(stored);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRuns(WorkflowRunId id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<NodeRun> rows = [.. _nodeRuns.Values
                .Where(n => n.RunId == id)
                .OrderBy(static n => n.NodeId.Value, StringComparer.Ordinal)
                .ThenBy(static n => n.ItemIndex)];
            return Ok(rows);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<NodeRun>>> NodeRunsForItem(WorkflowRunId id, int itemIndex, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<NodeRun> rows = [.. _nodeRuns.Values
                .Where(n => n.RunId == id && (n.ItemIndex == 0 || n.ItemIndex == itemIndex))
                .OrderBy(static n => n.NodeId.Value, StringComparer.Ordinal)
                .ThenBy(static n => n.ItemIndex)];
            return Ok(rows);
        }
    }

    private static string? KindOf(string trigger) => System.Text.Json.Nodes.JsonNode.Parse(trigger)?["kind"]?.GetValue<string>();

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static Error NotFound(WorkflowRunId id) => SindriErrors.GraphRunNotFound.With($"no run '{id.Value}'; list the workflow's runs to see what exists");
}
