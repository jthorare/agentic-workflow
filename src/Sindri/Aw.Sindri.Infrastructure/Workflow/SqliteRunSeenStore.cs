using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow_run_seen' in ledger.db (migration 22) beside 'workflow_run': which failed runs the owner has seen. A read of an older file finds nothing unseen and never migrates it.</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">The clock the seen times come from.</param>
internal sealed class SqliteRunSeenStore(LedgerDatabase database, TimeProvider clock) : IRunSeenStore
{
    private const string FailedUnseenSql = WorkflowRunSql.SelectRun +
        " WHERE mode = 'live' AND status = 'failed' AND NOT EXISTS (SELECT 1 FROM workflow_run_seen s WHERE s.run_id = workflow_run.id)" +
        " ORDER BY COALESCE(ended_at, started_at) DESC, id DESC LIMIT $limit";

    private const string MarkSeenSql = "INSERT OR IGNORE INTO workflow_run_seen (run_id, seen_at) VALUES ($run, $at)";

    public ValueTask<Result<IReadOnlyList<WorkflowRun>>> FailedUnseen(int limit, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, LedgerDatabase.WorkflowRunSeenVersion, [],
            (session, token) => RecipeSessions.List(session, new SqlStatement(FailedUnseenSql, c => c.Bind("$limit", Math.Max(limit, 1))), WorkflowRunMapper.Instance, token), cancellationToken);

    public ValueTask<Result<Unit>> MarkSeen(WorkflowRunId run, CancellationToken cancellationToken) =>
        RecipeSessions.Write(database, async ValueTask<Result<Unit>> (session, token) =>
        {
            var marked = await RecipeSessions.Execute(session, new SqlStatement(MarkSeenSql, c => c.Bind("$run", run.Value).Bind("$at", clock.GetUtcNow()), $"seen {run.Value}"), token).ConfigureAwait(false);
            return marked.IsError ? marked.Error : Unit.Value;
        }, cancellationToken);
}
