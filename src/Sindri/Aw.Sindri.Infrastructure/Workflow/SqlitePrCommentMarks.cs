using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// 'pr_comment_marks' in ledger.db (babysitter spec §5.4, W26): append-only rows per pull request comment. A read never
/// creates or migrates the file and finds no mark on a file older than the table.
/// </summary>
/// <param name="database">The ledger file.</param>
internal sealed class SqlitePrCommentMarks(LedgerDatabase database) : IPrCommentMarks
{
    public ValueTask<Result<bool>> Write(PrCommentMark mark, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mark);
        return RecipeSessions.Write(database, async ValueTask<Result<bool>> (session, token) =>
        {
            var statement = new SqlStatement(PrCommentMarkSql.Insert, c => Bound(c, mark), $"mark on {mark.Pr.Repo}#{mark.Pr.Number.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            var written = await RecipeSessions.Execute(session, statement, token).ConfigureAwait(false);
            return written.IsError ? written.Error : written.Value > 0;
        }, cancellationToken);
    }

    public ValueTask<Result<IReadOnlyList<PrCommentMark>>> Latest(PrRef pr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pr);
        return RecipeSessions.Read(database, LedgerDatabase.PrCommentMarksVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(PrCommentMarkSql.SelectLatest, c => ByPr(c, pr)), PrCommentMarkMapper.Instance, token), cancellationToken);
    }

    public ValueTask<Result<int>> Purge(PrRef pr, bool anotherInstanceNamesPr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pr);
        if (anotherInstanceNamesPr)
        {
            return ValueTask.FromResult<Result<int>>(0);
        }

        return RecipeSessions.Write(database, (session, token) =>
            RecipeSessions.Execute(session, new SqlStatement(PrCommentMarkSql.Purge, c => ByPr(c, pr), $"marks of {pr.Repo}"), token), cancellationToken);
    }

    private static SqliteCommand ByPr(SqliteCommand command, PrRef pr) => command
        .Bind("$forge", pr.Forge)
        .Bind("$repo", pr.Repo)
        .Bind("$pr", pr.Number);

    private static SqliteCommand Bound(SqliteCommand command, PrCommentMark mark) => ByPr(command, mark.Pr)
        .Bind("$commentId", mark.CommentId.Value)
        .Bind("$editedAt", mark.EditedAt ?? string.Empty)
        .Bind("$instance", mark.Instance.Value)
        .Bind("$outcome", mark.Outcome)
        .Bind("$reason", mark.Reason)
        .Bind("$commit", mark.Commit)
        .Bind("$at", mark.At)
        .Bind("$effect", mark.Effect);
}
