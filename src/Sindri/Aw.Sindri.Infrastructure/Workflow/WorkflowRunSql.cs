namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The run store's SQL (ledger.db, migration 0009_workflow_runs).</summary>
internal static class WorkflowRunSql
{
    /// <summary>A run row (columns in <see cref="WorkflowRunMapper"/> order).</summary>
    internal const string SelectRun = "SELECT id, workflow_id, revision, trigger, status, outputs, error, item_id, started_at, ended_at FROM workflow_run";

    internal const string ById = SelectRun + " WHERE id = $id";

    internal const string InFlight = SelectRun + " WHERE workflow_id = $workflow AND status IN ('queued', 'running', 'waiting') ORDER BY started_at, id";

    internal const string Recent = SelectRun + " WHERE workflow_id = $workflow ORDER BY started_at DESC, CAST(substr(id, instr(id, ':') + 1) AS INTEGER) DESC LIMIT $limit OFFSET $offset";
    internal const string Running = SelectRun + " WHERE status = 'running' ORDER BY started_at, id";
    internal const string QueuedWorkflows = "SELECT DISTINCT workflow_id FROM workflow_run WHERE status = 'queued' ORDER BY workflow_id";

    internal const string LatestOfKind = SelectRun + " WHERE workflow_id = $workflow AND json_extract(trigger, '$.kind') = $kind ORDER BY CAST(substr(id, instr(id, ':') + 1) AS INTEGER) DESC LIMIT 1";

    internal const string InsertRun =
        "INSERT INTO workflow_run (id, workflow_id, revision, trigger, status, outputs, error, item_id, started_at) VALUES ($id, $workflow, $revision, $trigger, $status, $outputs, $error, $item, $at)";

    internal const string SetItem = "UPDATE workflow_run SET item_id = $item WHERE id = $id AND item_id IS NULL";

    internal const string SetEnded = "UPDATE workflow_run SET ended_at = $at WHERE id = $id";

    private const string Update = "UPDATE workflow_run SET status = $status, outputs = COALESCE($outputs, outputs), error = $error, ended_at = $ended WHERE id = $id";

    // why: every change is conditional, so a run that already ended never moves again, whatever order two writers arrive in; the caller reads the row back to see whether its change applied.

    /// <summary>Any move of a run that has not ended.</summary>
    internal const string SetStatus = Update + " AND status NOT IN ('succeeded', 'failed', 'cancelled')";

    /// <summary>Putting a run back to queued: only a running one (a launch that never reached Temporal).</summary>
    internal const string Requeue = Update + " AND status = 'running'";

    /// <summary>Starting a queued run (promote) and cancelling one (a queued cancel): only a run that is still queued.</summary>
    internal const string FromQueued = Update + " AND status = 'queued'";

    /// <summary>Removing a run that never reached Temporal.</summary>
    internal const string Discard = "DELETE FROM workflow_run WHERE id = $id AND status = 'running'";

    internal const string UpsertTick =
        "INSERT INTO workflow_schedule_tick (workflow_id, last_tick) VALUES ($workflow, $tick) ON CONFLICT (workflow_id) DO UPDATE SET last_tick = excluded.last_tick WHERE excluded.last_tick > workflow_schedule_tick.last_tick";

    internal const string SelectTick = "SELECT last_tick FROM workflow_schedule_tick WHERE workflow_id = $workflow";

    internal const string UpsertNodeRun =
        "INSERT INTO workflow_node_run (run_id, node_id, item_index, status, input, output, truncated, error, usage, raw, started_at, ended_at) VALUES ($run, $node, $item, $status, $input, $output, $truncated, $error, $usage, $raw, $started, $ended) " +
        "ON CONFLICT (run_id, node_id, item_index) DO UPDATE SET status = excluded.status, input = excluded.input, output = excluded.output, truncated = excluded.truncated, error = excluded.error, usage = excluded.usage, raw = excluded.raw, started_at = COALESCE(workflow_node_run.started_at, excluded.started_at), ended_at = excluded.ended_at";

    internal const string SelectNodeRuns =
        "SELECT run_id, node_id, item_index, status, input, output, truncated, error, usage, raw, started_at, ended_at FROM workflow_node_run WHERE run_id = $run ORDER BY node_id, item_index";

    internal const string SelectNodeRunsForItem =
        "SELECT run_id, node_id, item_index, status, input, output, truncated, error, usage, raw, started_at, ended_at FROM workflow_node_run WHERE run_id = $run AND (item_index = 0 OR item_index = $item) ORDER BY node_id, item_index";
}
