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

    private const string Visible = "r.removed_at IS NULL AND r.blocked = 0 AND (r.audience_key IN (SELECT value FROM json_each(@audiences)) OR r.key IN (SELECT value FROM json_each(@records)))";

    private const string IssueColumns =
        "i.key, r.title, r.url, r.state_raw, r.author, r.created_at, r.closed_at, r.audience_key, " +
        "i.team_id, i.identifier, i.state_id, i.state_type, i.assignee_id, i.priority, i.estimate, i.cycle_id, i.project_id, i.parent_id, i.labels, i.due_date, i.sort_order, i.updated_at";

    /// <summary>Issues newest first by Linear's update time, then key; a page continues after (@afterAt, @afterKey). A filter that is NULL does not narrow.</summary>
    internal const string ListIssues =
        "SELECT " + IssueColumns + " FROM linear_issue i JOIN source_record r ON r.key = i.key WHERE " + Visible + " " +
        "AND (@team IS NULL OR r.audience_key = 'linear:' || @team) AND (@assignee IS NULL OR i.assignee_id = @assignee) " +
        "AND (@states IS NULL OR i.state_type IN (SELECT value FROM json_each(@states))) AND (@project IS NULL OR i.project_id = @project) AND (@cycle IS NULL OR i.cycle_id = @cycle) " +
        "AND (@afterAt IS NULL OR i.updated_at < @afterAt OR (i.updated_at = @afterAt AND i.key > @afterKey)) " +
        "ORDER BY i.updated_at DESC, i.key ASC LIMIT @limit";

    internal const string GetIssue = "SELECT " + IssueColumns + " FROM linear_issue i JOIN source_record r ON r.key = i.key WHERE i.key = @key AND " + Visible;

    internal const string IssueComments =
        "SELECT c.id, c.author_id, c.created_at, c.body, c.sindri FROM linear_comment c JOIN source_record r ON r.key = c.issue_key " +
        "WHERE c.issue_key = @key AND " + Visible + " ORDER BY c.created_at DESC, c.id ASC";

    /// <summary>Assignments to the viewer, and state moves a sync saw on issues assigned to them, after a marker.</summary>
    internal const string SinceChanges =
        "SELECT ch.issue_key, i.identifier, ch.field, ch.\"old\", ch.\"new\", ch.at, ch.via FROM linear_issue_change ch " +
        "JOIN linear_issue i ON i.key = ch.issue_key JOIN source_record r ON r.key = ch.issue_key " +
        "WHERE ch.at > @marker AND " + Visible + " " +
        "AND ((ch.field = 'assignee' AND ch.\"new\" = @viewer) OR (ch.field = 'state' AND ch.via = 'feed' AND i.assignee_id = @viewer)) ORDER BY ch.at ASC, ch.seq ASC";

    /// <summary>Comments after a marker that someone else wrote, newest first and capped: the mention test runs on them in code.</summary>
    internal const string SinceComments =
        "SELECT c.id, c.author_id, c.created_at, c.body, c.sindri, c.issue_key, i.identifier FROM linear_comment c " +
        "JOIN linear_issue i ON i.key = c.issue_key JOIN source_record r ON r.key = c.issue_key " +
        "WHERE c.created_at > @marker AND (c.author_id IS NULL OR c.author_id <> @viewer) AND c.sindri = 0 AND " + Visible + " ORDER BY c.created_at DESC, c.id ASC LIMIT @limit";

    internal const string ViewerNames = "SELECT name, display_name FROM linear_member WHERE id = @viewer";
}
