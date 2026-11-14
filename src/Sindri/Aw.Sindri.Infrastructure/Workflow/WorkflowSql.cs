namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>The workflow store's SQL (ledger.db, migration 0008_workflows).</summary>
internal static class WorkflowSql
{
    /// <summary>A workflow row with its newest revision number (columns in <see cref="WorkflowRecordMapper"/> order).</summary>
    internal const string SelectRecord =
        "SELECT w.id, w.slug, w.name, w.active_revision, (SELECT MAX(revision) FROM workflow_revision WHERE workflow_id = w.id), w.archived_at, w.created_by, w.was_activated FROM workflow w";

    internal const string ById = SelectRecord + " WHERE w.id = $id";

    internal const string ListAll = SelectRecord + " ORDER BY w.slug";

    internal const string ListLive = SelectRecord + " WHERE w.archived_at IS NULL ORDER BY w.slug";

    internal const string InsertWorkflow = "INSERT INTO workflow (id, slug, name, created_by) VALUES ($id, $slug, $name, $by)";

    internal const string InsertRevision =
        "INSERT INTO workflow_revision (workflow_id, revision, spec, spec_hash, created_by, created_at) VALUES ($id, $revision, $spec, $hash, $by, $at)";

    internal const string SlugTaken = "SELECT EXISTS (SELECT 1 FROM workflow WHERE slug = $slug)";

    internal const string SlugTakenByArchived = "SELECT EXISTS (SELECT 1 FROM workflow WHERE slug = $slug AND archived_at IS NOT NULL)";

    internal const string Rename = "UPDATE workflow SET name = $name WHERE id = $id";

    internal const string Pin = "UPDATE workflow SET active_revision = $revision, was_activated = 1 WHERE id = $id";

    internal const string Unpin = "UPDATE workflow SET active_revision = NULL WHERE id = $id";

    internal const string Archive = "UPDATE workflow SET archived_at = $at, active_revision = NULL WHERE id = $id";

    internal const string SelectRevision =
        "SELECT workflow_id, revision, spec, spec_hash, created_by, created_at FROM workflow_revision WHERE workflow_id = $id AND revision = COALESCE($revision, (SELECT MAX(revision) FROM workflow_revision WHERE workflow_id = $id))";
}
