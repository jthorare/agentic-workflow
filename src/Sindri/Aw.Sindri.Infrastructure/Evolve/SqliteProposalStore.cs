using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Kernel.Ids;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Evolve;

/// <summary>
/// The 'proposal', 'proposal_source' and 'proposal_public' tables of the ledger file (migration 23, issue 3787). A ledger older than 23 reads as no proposals.
/// The caller scrubs text before it saves; the store writes what it is given.
/// </summary>
/// <param name="database">The ledger file.</param>
/// <param name="ids">Makes new proposal ids.</param>
/// <param name="clock">Stamps the rows.</param>
internal sealed partial class SqliteProposalStore(LedgerDatabase database, IIdGenerator ids, TimeProvider clock) : IProposalStore
{
    private const int MaxEvidence = 20;

    public async ValueTask<Result<IReadOnlyList<StoredProposal>>> List(ProposalState? state, CancellationToken cancellationToken)
    {
        var statement = state is { } only
            ? new SqlStatement(ProposalSql.Select + " WHERE p.state = $state ORDER BY p.updated_at DESC, p.id DESC", c => c.Bind("$state", ProposalSql.Name(only)))
            : new SqlStatement(ProposalSql.Select + " ORDER BY p.updated_at DESC, p.id DESC");
        var rows = await Read(statement, cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success<IReadOnlyList<StoredProposal>>([.. rows.Value]);
    }

    public async ValueTask<Result<StoredProposal?>> Find(ProposalId id, CancellationToken cancellationToken)
    {
        var rows = await Read(ById(id), cancellationToken).ConfigureAwait(false);
        return rows.IsError ? rows.Error : Results.Success(rows.Value.Count == 0 ? null : rows.Value[0]);
    }

    public async ValueTask<Result<StoredProposal>> Reject(ProposalId id, CancellationToken cancellationToken)
    {
        var begun = await Begin(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return begun.Error;
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var found = await Rows(begun.Value, ById(id), cancellationToken).ConfigureAwait(false);
            if (found.IsError)
            {
                return found.Error;
            }

            if (found.Value.Count == 0)
            {
                return SindriErrors.ProposalNotFound.With(id.Value);
            }

            if (!found.Value[0].Rejectable)
            {
                return SindriErrors.ProposalNotRejectable.With($"{id.Value} is {ProposalSql.Name(found.Value[0].State)}");
            }

            var now = clock.GetUtcNow();
            var update = new SqlStatement(
                "UPDATE proposal SET state = $state, updated_at = $at WHERE id = $id",
                c => c.Bind("$state", ProposalSql.Name(ProposalState.Rejected)).Bind("$at", ProposalSql.Stamp(now)).Bind("$id", id.Value),
                $"proposal {id.Value}");
            var written = await LeaseReads.WriteAndCommit(begun.Value, update, cancellationToken).ConfigureAwait(false);
            return written.IsError ? written.Error : found.Value[0] with { State = ProposalState.Rejected, UpdatedAt = now };
        }
    }

    private static SqlStatement ById(ProposalId id) => new(ProposalSql.Select + " WHERE p.id = $id", c => c.Bind("$id", id.Value));

    private async ValueTask<Result<SqliteUnitOfWork>> Begin(CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        return begun.IsError ? LedgerDatabase.Translate(begun.Error) : begun.Value;
    }

    private async ValueTask<Result<IReadOnlyList<StoredProposal>>> Read(SqlStatement statement, CancellationToken cancellationToken)
    {
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied < LedgerDatabase.ProposalsVersion)
        {
            return schema.IsError ? schema.Error : Results.Success<IReadOnlyList<StoredProposal>>([]);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            return await Rows(read.Value, statement, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<Result<IReadOnlyList<StoredProposal>>> Rows(ISqliteSession session, SqlStatement statement, CancellationToken cancellationToken)
    {
        var rows = await SqliteQueries.List(session, statement, ProposalSql.RowMapper.Instance, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return LedgerDatabase.Translate(rows.Error);
        }

        var stored = new List<StoredProposal>(rows.Value.Count);
        foreach (var row in rows.Value)
        {
            var parsed = ProposalSql.ToStored(row);
            if (parsed.IsError)
            {
                return SindriErrors.LedgerUnavailable.With($"proposal {row.Id} doesn't parse: {parsed.Error.Message}");
            }

            stored.Add(parsed.Value);
        }

        return stored;
    }
}
