using System.Text;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel.Data;
using Aw.Kernel.Json;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>The row mappers of the Linear side tables, shared by the side-row writer (which compares what it stored) and the issue reads.</summary>
internal static class LinearRowReads
{
    /// <summary>The 14 columns of <see cref="LinearSql.StoredIssue"/>: team_id to updated_at.</summary>
    internal static IRowMapper<LinearIssueFields> Fields { get; } = new Mapper<LinearIssueFields>(r => ReadFields(r, 0));

    /// <summary>The five columns of <see cref="LinearSql.StoredComments"/>: id to sindri.</summary>
    internal static IRowMapper<LinearCommentRow> Comment { get; } = new Mapper<LinearCommentRow>(r => ReadComment(r, 0));

    /// <summary>The issue fields in 14 columns from <paramref name="first"/>: team_id, identifier, state_id, state_type, assignee_id, priority, estimate, cycle_id, project_id, parent_id, labels, due_date, sort_order, updated_at.</summary>
    internal static LinearIssueFields ReadFields(SqliteDataReader r, int first) => new(
        Id(r, first), r.GetString(first + 1), Id(r, first + 2), Text(r, first + 3), Id(r, first + 4), r.GetInt32(first + 5), r.IsDBNull(first + 6) ? null : r.GetDouble(first + 6),
        Id(r, first + 7), Id(r, first + 8), Id(r, first + 9), Labels(r.GetString(first + 10)), Text(r, first + 11), r.IsDBNull(first + 12) ? null : r.GetDouble(first + 12), SqliteRead.GetTimestamp(r, first + 13));

    /// <summary>A comment in five columns from <paramref name="first"/>: id, author_id, created_at, body, sindri.</summary>
    internal static LinearCommentRow ReadComment(SqliteDataReader r, int first) =>
        new(ExternalId.From(r.GetString(first)), Id(r, first + 1), SqliteRead.GetTimestamp(r, first + 2), r.GetString(first + 3), r.GetInt64(first + 4) == 1);

    internal static ExternalId? Id(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : ExternalId.From(r.GetString(ordinal));

    internal static string? Text(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    internal static DateTimeOffset? At(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : SqliteRead.GetTimestamp(r, ordinal);

    private static IReadOnlyList<ExternalId> Labels(string json) =>
        AwJson.Deserialize<string[]>(Encoding.UTF8.GetBytes(json), DesignDocJson.Options) is { IsError: false } read ? [.. read.Value.Select(ExternalId.From)] : [];

    private sealed class Mapper<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => map(reader);
    }
}
