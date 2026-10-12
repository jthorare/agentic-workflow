using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Infrastructure.Ledger;

/// <summary>The host lease as one row in the ledger file, decided by <see cref="LeaseDecision"/> inside a write transaction.</summary>
internal sealed class SqliteLease(LedgerDatabase database, IProcessProbe probe, TimeProvider clock) : ILease
{
    public async ValueTask<Result<LeaseGrant>> Acquire(LeaseHolder candidate, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var row = await LeaseReads.Current(begun.Value, cancellationToken).ConfigureAwait(false);
            if (row.IsError)
            {
                return row.Error;
            }

            var now = clock.GetUtcNow();
            var liveness = row.Value is { Released: false } held
                ? LeaseDecision.Liveness(held.Holder, probe.Self(), probe.Facts(held.Holder.Pid))
                : HolderLiveness.Unknown;
            var decision = LeaseDecision.Acquire(row.Value, candidate, now, liveness);
            if (decision.IsError)
            {
                return decision.Error;
            }

            var committed = await LeaseReads.WriteAndCommit(begun.Value, Upsert(decision.Value, now), cancellationToken).ConfigureAwait(false);
            return committed.IsError ? committed.Error : decision;
        }
    }

    public ValueTask<Result<LeaseGrant>> Renew(LeaseGrant grant, CancellationToken cancellationToken) =>
        Change(grant, renew: true, cancellationToken);

    public async ValueTask<Result<Unit>> Release(LeaseGrant grant, CancellationToken cancellationToken)
    {
        var released = await Change(grant, renew: false, cancellationToken).ConfigureAwait(false);
        return released.IsError ? released.Error : Unit.Value;
    }

    public async ValueTask<Result<LeaseRow?>> Read(CancellationToken cancellationToken)
    {
        var schema = await database.Schema(cancellationToken).ConfigureAwait(false);
        if (schema.IsError || schema.Value.Applied < 2)
        {
            return schema.IsError ? schema.Error : Results.Success<LeaseRow?>(value: null);
        }

        var read = await database.Sessions.BeginRead(cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return LedgerDatabase.Translate(read.Error);
        }

        await using (read.Value.ConfigureAwait(false))
        {
            return await LeaseReads.Current(read.Value, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<LeaseGrant>> Change(LeaseGrant grant, bool renew, CancellationToken cancellationToken)
    {
        var writable = await database.Writable(cancellationToken).ConfigureAwait(false);
        if (writable.IsError)
        {
            return writable.Error;
        }

        var begun = await writable.Value.BeginSession(cancellationToken).ConfigureAwait(false);
        if (begun.IsError)
        {
            return LedgerDatabase.Translate(begun.Error);
        }

        await using (begun.Value.ConfigureAwait(false))
        {
            var row = await LeaseReads.Current(begun.Value, cancellationToken).ConfigureAwait(false);
            var now = clock.GetUtcNow();
            var decided = row.IsError ? row.Error : LeaseDecision.Renew(row.Value, grant, now);
            if (decided.IsError)
            {
                return decided.Error;
            }

            var statement = renew
                ? new SqlStatement(LedgerSql.RenewLease, c => c.Bind("$expiresAt", decided.Value.ExpiresAt).Bind("$epoch", grant.Epoch))
                : new SqlStatement(LedgerSql.ReleaseLease, c => c.Bind("$now", now).Bind("$epoch", grant.Epoch));
            var committed = await LeaseReads.WriteAndCommit(begun.Value, statement, cancellationToken).ConfigureAwait(false);
            return committed.IsError ? committed.Error : decided;
        }
    }

    private static SqlStatement Upsert(LeaseGrant grant, DateTimeOffset now) => new(LedgerSql.UpsertLease, c => c
        .Bind("$host", grant.Holder.Host.Value)
        .Bind("$pid", grant.Holder.Pid)
        .Bind("$pidStart", grant.Holder.PidStart)
        .Bind("$bootId", grant.Holder.BootId.Value)
        .Bind("$epoch", grant.Epoch)
        .Bind("$acquiredAt", now)
        .Bind("$expiresAt", grant.ExpiresAt), string.Create(CultureInfo.InvariantCulture, $"lease epoch {grant.Epoch}"));
}
