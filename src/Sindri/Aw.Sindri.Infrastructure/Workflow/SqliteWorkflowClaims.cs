using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Infrastructure.Ledger;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// 'workflow_claims' in ledger.db (engine spec §5.9; engine P6 unit (d)): one row per item naming its holder. A take reads
/// and writes inside one write transaction, so two takes of a free item can't both win; a read never creates or
/// migrates the file and finds no claim on a file older than the table.
/// </summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">Stamps a claim.</param>
internal sealed class SqliteWorkflowClaims(LedgerDatabase database, TimeProvider clock) : IWorkflowClaims
{
    public ValueTask<Result<WorkflowClaim?>> Find(ItemKey item, CancellationToken cancellationToken) =>
        RecipeSessions.Read(database, LedgerDatabase.ClaimsVersion, empty: null,
            (session, token) => RecipeSessions.First(session, ByItem(item), WorkflowClaimMapper.Instance, token), cancellationToken);

    public ValueTask<Result<WorkflowClaim>> Take(WorkflowClaim claim, InstanceAddress? ended, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return RecipeSessions.Write(database, async ValueTask<Result<WorkflowClaim>> (session, token) =>
        {
            var holder = await RecipeSessions.First(session, ByItem(claim.Item), WorkflowClaimMapper.Instance, token).ConfigureAwait(false);
            if (holder.IsError)
            {
                return holder.Error;
            }

            SqlStatement write;
            if (holder.Value is not { } held)
            {
                write = Insert(claim);
            }
            else if (ended is { } gone && held.Owner == gone)
            {
                write = Replace(claim, gone);
            }
            else
            {
                return held;   // the same owner (taken before) or another instance's
            }

            var written = await RecipeSessions.Execute(session, write, token).ConfigureAwait(false);
            return written.IsError ? written.Error : claim;
        }, cancellationToken);
    }

    private static SqlStatement ByItem(ItemKey item) => new(ClaimSql.SelectClaim, c => c.Bind("$item", item.Value));

    private SqlStatement Insert(WorkflowClaim claim) => new(ClaimSql.InsertClaim, c => Bound(c, claim), $"claim on {claim.Item.Value}");

    private SqlStatement Replace(WorkflowClaim claim, InstanceAddress ended) =>
        new(ClaimSql.ReplaceEndedClaim, c => Bound(c, claim).Bind("$ended", ended.Value), $"claim on {claim.Item.Value}");

    private SqliteCommand Bound(SqliteCommand command, WorkflowClaim claim) => command
        .Bind("$item", claim.Item.Value)
        .Bind("$owner", claim.Owner.Value)
        .Bind("$attempt", claim.Attempt)
        .Bind("$claimedAt", clock.GetUtcNow());
}
