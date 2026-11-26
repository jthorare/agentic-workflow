using Aw.Kernel;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Testing.Workflow;

// The filtered runs read (#4334), as the SQLite store's query keeps it.
public sealed partial class InMemoryWorkflowRunStore
{
    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> Find(WorkflowId workflow, WorkflowRunQuery query, int offset, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        lock (_gate)
        {
            IReadOnlyList<WorkflowRun> found = [.. _runs
                .Where(r => r.WorkflowId == workflow && string.Equals(r.Mode, WorkflowRunMode.Live, StringComparison.Ordinal) && Keeps(query, r))
                .OrderByDescending(static r => r.StartedAt)
                .ThenByDescending(static r => r.Id.Occurrence)
                .Skip(Math.Max(offset, 0))
                .Take(Math.Max(limit, 1))];
            return Ok(found);
        }
    }

    private static bool Keeps(WorkflowRunQuery query, WorkflowRun run) =>
        (query.Statuses.Count == 0 || query.Statuses.Contains(run.Status))
        && (query.Since is not { } since || run.StartedAt >= since)
        && (query.Until is not { } until || run.StartedAt < until)
        && (query.Before is not { } before || run.StartedAt < before.StartedAt || (run.StartedAt == before.StartedAt && run.Id.Occurrence < before.Id.Occurrence));
}
