using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IWorkflowRunStore"/>. Passes <see cref="WorkflowRunStoreContract"/>.</summary>
/// <param name="clock">The clock the end times come from.</param>
public sealed class InMemoryWorkflowRunStore(TimeProvider clock) : IWorkflowRunStore
{
    private readonly Lock _gate = new();
    private readonly List<WorkflowRun> _runs = [];
    private readonly Dictionary<(WorkflowRunId Run, NodeId Node, int Item), NodeRun> _nodeRuns = [];

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> Insert(WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
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

    /// <inheritdoc/>
    public ValueTask<Result<int?>> LatestOccurrence(WorkflowId workflow, string triggerKind, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var matching = _runs.Where(r => r.WorkflowId == workflow && KindOf(r.Trigger) == triggerKind).ToList();
            int? latest = matching.Count == 0 ? null : matching.Max(static r => r.Id.Occurrence);
            return Ok(latest);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorkflowRun>> SetStatus(WorkflowRunId id, WorkflowRunStatus status, string? outputs, string? error, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var at = _runs.FindIndex(r => r.Id == id);
            if (at < 0)
            {
                return Done<WorkflowRun>(NotFound(id));
            }

            var final = status is WorkflowRunStatus.Succeeded or WorkflowRunStatus.Failed or WorkflowRunStatus.Cancelled;
            _runs[at] = _runs[at] with { Status = status, Outputs = outputs ?? _runs[at].Outputs, Error = error, EndedAt = final ? clock.GetUtcNow() : null };
            return Done<WorkflowRun>(_runs[at]);
        }
    }

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

    private static string? KindOf(string trigger) => System.Text.Json.Nodes.JsonNode.Parse(trigger)?["kind"]?.GetValue<string>();

    private static ValueTask<Result<T>> Ok<T>(T value) => ValueTask.FromResult<Result<T>>(value);

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private static Error NotFound(WorkflowRunId id) => SindriErrors.GraphRunNotFound.With($"no run '{id.Value}'; list the workflow's runs to see what exists");
}
