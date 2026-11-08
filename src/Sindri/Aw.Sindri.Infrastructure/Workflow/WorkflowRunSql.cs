namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The run store's SQL (ledger.db, migration 0009_workflow_runs).</summary>
internal static class WorkflowRunSql
{
    /// <summary>A run row (columns in <see cref="WorkflowRunMapper"/> order).</summary>
    internal const string SelectRun = "SELECT id, workflow_id, revision, trigger, status, outputs, error, item_id, started_at, ended_at FROM workflow_run";

    internal const string ById = SelectRun + " WHERE id = $id";

    internal const string InFlight = SelectRun + " WHERE workflow_id = $workflow AND status IN ('queued', 'running', 'waiting') ORDER BY started_at, id";

    internal const string InsertRun =
        "INSERT INTO workflow_run (id, workflow_id, revision, trigger, status, outputs, error, item_id, started_at) VALUES ($id, $workflow, $revision, $trigger, $status, $outputs, $error, $item, $at)";

    internal const string SetEnded = "UPDATE workflow_run SET ended_at = $at WHERE id = $id";

    internal const string SetStatus = "UPDATE workflow_run SET status = $status, outputs = COALESCE($outputs, outputs), error = $error, ended_at = $ended WHERE id = $id";

    internal const string UpsertNodeRun =
        "INSERT INTO workflow_node_run (run_id, node_id, item_index, status, input, output, truncated, error, usage) VALUES ($run, $node, $item, $status, $input, $output, $truncated, $error, $usage) " +
        "ON CONFLICT (run_id, node_id, item_index) DO UPDATE SET status = excluded.status, input = excluded.input, output = excluded.output, truncated = excluded.truncated, error = excluded.error, usage = excluded.usage";

    internal const string SelectNodeRuns =
        "SELECT run_id, node_id, item_index, status, input, output, truncated, error, usage FROM workflow_node_run WHERE run_id = $run ORDER BY node_id, item_index";
}
