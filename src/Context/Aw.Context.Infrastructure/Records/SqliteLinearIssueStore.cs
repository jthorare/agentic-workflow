using Aw.Context.Core.Linear;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Infrastructure.Records;

/// <summary>
/// The stored Linear issues, their comments and changes read from context.db (migration 0032_s1l_linear_hub, Linear hub spec 2026-11-04 §4.3). Every statement joins the issue's record, so the caller's audience, a tombstone and a withheld
/// record apply to the side tables exactly as they do to the record.
/// </summary>
/// <param name="database">The context database.</param>
internal sealed class SqliteLinearIssueStore(ContextDatabase database) : ILinearIssueStore
{
    private const int MentionCandidates = 1000;

    public async ValueTask<Result<LinearIssuePage>> List(LinearIssueFilter filter, LinearIssueCursor? after, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(audience);
        var limit = Math.Clamp(filter.Limit, 1, LinearIssueFilter.MaxLimit);
        var states = filter.StateTypes is { Count: > 0 } types ? SqliteLexicalLeg.JsonArray(types) : null;
        var statement = new SqlStatement(LinearSql.ListIssues, c => Audience(c, audience)
            .Bind("@team", filter.Team)
            .Bind("@assignee", filter.Assignee?.Value)
            .Bind("@states", states)
            .Bind("@project", filter.Project?.Value)
            .Bind("@cycle", filter.Cycle?.Value)
            .BindAt("@afterAt", after?.UpdatedAt)
            .Bind("@afterKey", after?.Key.Value)
            .Bind("@limit", limit + 1));
        var rows = await Read(statement, IssueMapper, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var page = rows.Value.Take(limit).ToList();   // one more than the limit was asked for: its presence says there is a next page
        var more = rows.Value.Count > limit;
        return new LinearIssuePage(page, more ? new LinearIssueCursor(page[^1].Fields.UpdatedAt, page[^1].Key) : null);
    }

    public async ValueTask<Result<LinearIssueRow?>> Get(RecordKey key, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var rows = await Read(new SqlStatement(LinearSql.GetIssue, c => Audience(c, audience).Bind("@key", key.Value)), IssueMapper, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public ValueTask<Result<LinearIssueRow?>> Get(LinearIssueKey key, RecordAudience audience, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<LinearIssueCounts>> Count(LinearIssueFilter filter, RecordAudience audience, CancellationToken cancellationToken) => throw new NotSupportedException();

    public async ValueTask<Result<IReadOnlyList<LinearCommentRow>>> Comments(RecordKey key, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        return await Read(new SqlStatement(LinearSql.IssueComments, c => Audience(c, audience).Bind("@key", key.Value)), LinearRowReads.Comment, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<Result<LinearSince>> Since(DateTimeOffset marker, ExternalId viewer, RecordAudience audience, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audience);
        var changes = await Read(new SqlStatement(LinearSql.SinceChanges, c => Audience(c, audience).Bind("@marker", marker).Bind("@viewer", viewer.Value)), ChangeMapper, cancellationToken).ConfigureAwait(false);
        var names = changes.IsError ? changes.Error : await Read(new SqlStatement(LinearSql.ViewerNames, c => c.Bind("@viewer", viewer.Value)), NamesMapper, cancellationToken).ConfigureAwait(false);
        var comments = names.IsError ? names.Error : await Read(new SqlStatement(LinearSql.SinceComments, c => Audience(c, audience).Bind("@marker", marker).Bind("@viewer", viewer.Value).Bind("@limit", MentionCandidates)), MentionMapper, cancellationToken).ConfigureAwait(false);
        if (comments.IsError)
        {
            return comments.Error;
        }

        var spellings = names.Value.SelectMany(static n => n).ToList();
        var mentions = comments.Value.Where(m => LinearMentions.Mentions(m.Comment.Body, spellings)).OrderBy(static m => m.Comment.CreatedAt).ThenBy(static m => m.Comment.Id.Value, StringComparer.Ordinal).ToList();
        return new LinearSince(changes.Value, mentions);
    }

    private static SqliteCommand Audience(SqliteCommand command, RecordAudience audience) => command
        .Bind("@audiences", SqliteLexicalLeg.JsonArray(audience.Allowed.Select(static k => k.Value)))
        .Bind("@records", SqliteLexicalLeg.JsonArray(audience.Records.Select(static k => k.Value)));

    private async ValueTask<Result<IReadOnlyList<T>>> Read<T>(SqlStatement statement, IRowMapper<T> mapper, CancellationToken cancellationToken)
    {
        var readable = await database.Readable(cancellationToken).ConfigureAwait(false);
        if (readable.IsError || readable.Value is not { } sessions)
        {
            return readable.IsError ? readable.Error : Results.Success<IReadOnlyList<T>>([]);
        }

        var read = await sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ContextDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            var rows = await SqliteQueries.List(read.Value, statement, mapper, cancellationToken).ConfigureAwait(false);
            return rows.IsError ? ContextDatabase.Translate(rows.Error) : rows;
        }
    }

    private static IRowMapper<LinearIssueRow> IssueMapper { get; } = new Mapper<LinearIssueRow>(r => new LinearIssueRow(
        RecordKey.From(r.GetString(0)), r.GetString(1), LinearRowReads.Text(r, 2), LinearRowReads.Text(r, 3), LinearRowReads.Text(r, 4), LinearRowReads.At(r, 5), LinearRowReads.At(r, 6),
        TeamOf(LinearRowReads.Text(r, 7)), LinearRowReads.ReadFields(r, 8), LinearRowReads.Text(r, 22)));

    private static IRowMapper<LinearSinceChange> ChangeMapper { get; } = new Mapper<LinearSinceChange>(r => new LinearSinceChange(
        RecordKey.From(r.GetString(0)), r.GetString(1), r.GetString(2), LinearRowReads.Id(r, 3), LinearRowReads.Id(r, 4), SqliteRead.GetTimestamp(r, 5), r.GetString(6)));

    private static IRowMapper<LinearSinceMention> MentionMapper { get; } = new Mapper<LinearSinceMention>(r =>
        new LinearSinceMention(RecordKey.From(r.GetString(5)), r.GetString(6), LinearRowReads.ReadComment(r, 0)));

    private static IRowMapper<string[]> NamesMapper { get; } = new Mapper<string[]>(r => [r.GetString(0), LinearRowReads.Text(r, 1) ?? string.Empty]);

    // why: the feed gives an issue the audience 'linear:<team key>'; any other audience has no team to name
    private static string? TeamOf(string? audience) => audience is not null && audience.StartsWith("linear:", StringComparison.Ordinal) ? audience["linear:".Length..] : null;

    private sealed class Mapper<T>(Func<SqliteDataReader, T> map) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => map(reader);
    }
}
