using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// The writer of <see cref="LinearSideRows"/> (Linear hub spec 2026-11-04 §4.1): <c language="text">linear_issue</c>, <c language="text">linear_comment</c> and <c language="text">linear_issue_change</c>, migration 0032. A replace logs a change row for
/// the assignee and for the state when the new fields differ from the stored ones; an issue with no stored fields (the first ';f3' re-save) logs none. Rows older than <see cref="ChangeRetention"/> are deleted by the same replace.
/// </summary>
/// <param name="clock">The clock the change rows' times and the retention cut-off come from.</param>
internal sealed class LinearRowWriter(TimeProvider? clock = null) : ISideRowWriter
{
    /// <summary>How long a change row is kept.</summary>
    internal static readonly TimeSpan ChangeRetention = TimeSpan.FromDays(30);

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public string Id => LinearSideRows.WriterId;

    public async ValueTask<Result<bool>> Differs(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken)
    {
        var wanted = ((LinearSideRows)rows).Issue;
        var stored = await StoredFields(session, key, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        if (stored.Value is not { } fields || !FieldsEqual(fields, wanted.Fields))
        {
            return true;
        }

        var comments = await SqliteQueries.List(session, new SqlStatement(LinearSql.StoredComments, c => c.Bind("@key", key.Value)), LinearRowReads.Comment, cancellationToken).ConfigureAwait(false);
        if (comments.IsError)
        {
            return ContextDatabase.Translate(comments.Error);
        }

        var description = await SqliteQueries.List(session, new SqlStatement(LinearSql.StoredDescription, c => c.Bind("@key", key.Value)), SideRowReads.Text, cancellationToken).ConfigureAwait(false);
        return description.IsError
            ? ContextDatabase.Translate(description.Error)
            : !CommentsEqual(comments.Value, wanted.Comments) || !string.Equals(description.Value.Count > 0 ? description.Value[0] : string.Empty, wanted.Description ?? string.Empty, StringComparison.Ordinal);
    }

    public async ValueTask<Error?> Replace(ISqliteSession session, RecordKey key, SideRows rows, CancellationToken cancellationToken)
    {
        var side = (LinearSideRows)rows;
        var stored = await StoredFields(session, key, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        return await UpsertIssue(session, key, side.Issue, cancellationToken).ConfigureAwait(false)
            ?? await ReplaceComments(session, key, side.Issue.Comments, cancellationToken).ConfigureAwait(false)
            ?? await LogChanges(session, key, stored.Value, side, cancellationToken).ConfigureAwait(false)
            ?? await SqliteRecordStore.Exec(session, LinearSql.PruneChanges, c => c.Bind("@cutoff", _clock.GetUtcNow() - ChangeRetention), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Error?> Delete(ISqliteSession session, RecordKey key, CancellationToken cancellationToken) =>
        await SqliteRecordStore.Exec(session, LinearSql.DeleteComments, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false)
            ?? await SqliteRecordStore.Exec(session, LinearSql.DeleteChanges, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false)
            ?? await SqliteRecordStore.Exec(session, LinearSql.DeleteIssue, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false);

    private static ValueTask<Error?> UpsertIssue(ISqliteSession session, RecordKey key, LinearIssueSide issue, CancellationToken cancellationToken) =>
        SqliteRecordStore.Exec(session, LinearSql.UpsertIssue, c => Bind(c, issue.Fields, issue.Description)
            .Bind("@key", key.Value), cancellationToken);

    private static SqliteCommand Bind(SqliteCommand c, LinearIssueFields f, string? description) => c
            .Bind("@team", f.TeamId?.Value)
            .Bind("@identifier", f.Identifier)
            .Bind("@stateId", f.StateId?.Value)
            .Bind("@stateType", f.StateType)
            .Bind("@assignee", f.AssigneeId?.Value)
            .Bind("@priority", f.Priority)
            .Bind("@estimate", f.Estimate)
            .Bind("@cycle", f.CycleId?.Value)
            .Bind("@project", f.ProjectId?.Value)
            .Bind("@parent", f.ParentId?.Value)
            .Bind("@labels", Labels(f.LabelIds))
            .Bind("@due", f.DueDate)
            .Bind("@sortOrder", f.SortOrder)
            .Bind("@updated", f.UpdatedAt)
            .Bind("@description", description);

    private static async ValueTask<Error?> ReplaceComments(ISqliteSession session, RecordKey key, IReadOnlyList<LinearCommentRow> comments, CancellationToken cancellationToken)
    {
        var failure = await SqliteRecordStore.Exec(session, LinearSql.DeleteComments, c => c.Bind("@key", key.Value), cancellationToken).ConfigureAwait(false);
        foreach (var comment in comments.Take(LinearIssueSide.MaxComments))
        {
            if (failure is not null)
            {
                return failure;
            }

            failure = await SqliteRecordStore.Exec(session, LinearSql.InsertComment, c => c
                .Bind("@id", comment.Id.Value)
                .Bind("@key", key.Value)
                .Bind("@author", comment.AuthorId?.Value)
                .Bind("@created", comment.CreatedAt)
                .Bind("@body", comment.Body)
                .Bind("@sindri", comment.Sindri), cancellationToken).ConfigureAwait(false);
        }

        return failure;
    }

    // why: no stored fields means the issue's first side rows (the ';f3' re-save, or a tombstoned issue that came back), and a first sight is not a change
    private async ValueTask<Error?> LogChanges(ISqliteSession session, RecordKey key, LinearIssueFields? before, LinearSideRows side, CancellationToken cancellationToken)
    {
        if (before is null)
        {
            return null;
        }

        var after = side.Issue.Fields;
        var at = _clock.GetUtcNow();
        if (before.AssigneeId != after.AssigneeId
            && await Log(session, key, new Change("assignee", before.AssigneeId, after.AssigneeId, side.Written.HasFlag(LinearWrittenFields.Assignee)), at, cancellationToken).ConfigureAwait(false) is { } assignee)
        {
            return assignee;
        }

        return before.StateId != after.StateId
            ? await Log(session, key, new Change("state", before.StateId, after.StateId, side.Written.HasFlag(LinearWrittenFields.State)), at, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private static ValueTask<Error?> Log(ISqliteSession session, RecordKey key, Change change, DateTimeOffset at, CancellationToken cancellationToken) =>
        SqliteRecordStore.Exec(session, LinearSql.InsertChange, c => c
            .Bind("@key", key.Value)
            .Bind("@field", change.Field)
            .Bind("@old", change.Old?.Value)
            .Bind("@new", change.New?.Value)
            .Bind("@at", at)
            .Bind("@via", change.Written ? "write-through" : "feed"), cancellationToken);

    private static async ValueTask<Result<LinearIssueFields?>> StoredFields(ISqliteSession session, RecordKey key, CancellationToken cancellationToken)
    {
        var rows = await SqliteQueries.List(session, new SqlStatement(LinearSql.StoredIssue, c => c.Bind("@key", key.Value)), LinearRowReads.Fields, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return ContextDatabase.Translate(rows.Error);
        }

        return Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    private static bool FieldsEqual(LinearIssueFields a, LinearIssueFields b) => SameWhere(a, b) && SameWhat(a, b);

    private static bool SameWhere(LinearIssueFields a, LinearIssueFields b) =>
        a.TeamId == b.TeamId && a.StateId == b.StateId && a.AssigneeId == b.AssigneeId && a.CycleId == b.CycleId && a.ProjectId == b.ProjectId && a.ParentId == b.ParentId && a.LabelIds.SequenceEqual(b.LabelIds);

    private static bool SameWhat(LinearIssueFields a, LinearIssueFields b) =>
        string.Equals(a.Identifier, b.Identifier, StringComparison.Ordinal) && string.Equals(a.StateType, b.StateType, StringComparison.Ordinal) && string.Equals(a.DueDate, b.DueDate, StringComparison.Ordinal)
        && a.Priority == b.Priority && a.Estimate == b.Estimate && a.SortOrder == b.SortOrder && a.UpdatedAt == b.UpdatedAt;

    private static bool CommentsEqual(IReadOnlyList<LinearCommentRow> stored, IReadOnlyList<LinearCommentRow> wanted)
    {
        var sent = wanted.Take(LinearIssueSide.MaxComments).ToList();
        return stored.Count == sent.Count && stored.Zip(sent).All(static p =>
            p.First.Id == p.Second.Id && p.First.AuthorId == p.Second.AuthorId && p.First.CreatedAt == p.Second.CreatedAt
            && string.Equals(p.First.Body, p.Second.Body, StringComparison.Ordinal) && p.First.Sindri == p.Second.Sindri);
    }

    private static string Labels(IReadOnlyList<ExternalId> ids) => DesignDocJson.Strings([.. ids.Select(static i => i.Value)]);

    private sealed record Change(string Field, ExternalId? Old, ExternalId? New, bool Written);
}
