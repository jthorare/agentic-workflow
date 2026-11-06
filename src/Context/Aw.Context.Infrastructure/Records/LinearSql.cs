namespace Aw.Context.Infrastructure.Records;

/// <summary>The Linear hub's side-row SQL (migration 0032_linear_hub). Values are always bound, never formatted in.</summary>
internal static class LinearSql
{
    internal const string StoredIssue =
        "SELECT team_id, identifier, state_id, state_type, assignee_id, priority, estimate, cycle_id, project_id, parent_id, labels, due_date, sort_order, updated_at FROM linear_issue WHERE key = @key";

    internal const string StoredComments =
        "SELECT id, author_id, created_at, body, sindri FROM linear_comment WHERE issue_key = @key ORDER BY created_at DESC, id ASC";

    internal const string UpsertIssue =
        "INSERT INTO linear_issue (key, team_id, identifier, state_id, state_type, assignee_id, priority, estimate, cycle_id, project_id, parent_id, labels, due_date, sort_order, updated_at) " +
        "VALUES (@key, @team, @identifier, @stateId, @stateType, @assignee, @priority, @estimate, @cycle, @project, @parent, @labels, @due, @sortOrder, @updated) " +
        "ON CONFLICT (key) DO UPDATE SET team_id = excluded.team_id, identifier = excluded.identifier, state_id = excluded.state_id, state_type = excluded.state_type, assignee_id = excluded.assignee_id, " +
        "priority = excluded.priority, estimate = excluded.estimate, cycle_id = excluded.cycle_id, project_id = excluded.project_id, parent_id = excluded.parent_id, labels = excluded.labels, " +
        "due_date = excluded.due_date, sort_order = excluded.sort_order, updated_at = excluded.updated_at";

    internal const string DeleteComments = "DELETE FROM linear_comment WHERE issue_key = @key";

    internal const string InsertComment =
        "INSERT INTO linear_comment (id, issue_key, author_id, created_at, body, sindri) VALUES (@id, @key, @author, @created, @body, @sindri) " +
        "ON CONFLICT (id) DO UPDATE SET issue_key = excluded.issue_key, author_id = excluded.author_id, created_at = excluded.created_at, body = excluded.body, sindri = excluded.sindri";

    internal const string InsertChange = "INSERT INTO linear_issue_change (issue_key, field, \"old\", \"new\", at, via) VALUES (@key, @field, @old, @new, @at, @via)";

    internal const string PruneChanges = "DELETE FROM linear_issue_change WHERE at < @cutoff";

    internal const string DeleteChanges = "DELETE FROM linear_issue_change WHERE issue_key = @key";

    internal const string DeleteIssue = "DELETE FROM linear_issue WHERE key = @key";
}
