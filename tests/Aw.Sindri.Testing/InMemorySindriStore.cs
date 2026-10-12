using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory lease and ledger, for unit tests; passes <see cref="LeaseContract"/> and <see cref="LedgerStoreContract"/>.</summary>
/// <param name="clock">The clock leases expire by.</param>
/// <param name="probe">What the store may learn about a holder's process.</param>
public sealed class InMemorySindriStore(TimeProvider clock, IProcessProbe probe) : ILease, ILedgerStore
{
    private readonly Lock _gate = new();
    private readonly List<LedgerEvent> _events = [];
    private LeaseRow? _row;

    /// <summary>The events recorded so far, oldest first.</summary>
    public IReadOnlyList<LedgerEvent> Events { get { lock (_gate) { return [.. _events]; } } }

    /// <inheritdoc/>
    public ValueTask<Result<LeaseGrant>> Acquire(LeaseHolder candidate, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var now = clock.GetUtcNow();
            var liveness = _row is { Released: false } row
                ? LeaseDecision.Liveness(row.Holder, probe.Self(), probe.Facts(row.Holder.Pid))
                : HolderLiveness.Unknown;
            var decision = LeaseDecision.Acquire(_row, candidate, now, liveness);
            if (decision.IsSuccess)
            {
                _row = new LeaseRow(candidate, decision.Value.Epoch, now, decision.Value.ExpiresAt, Released: false);
            }

            return ValueTask.FromResult(decision);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<LeaseGrant>> Renew(LeaseGrant grant, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var renewed = LeaseDecision.Renew(_row, grant, clock.GetUtcNow());
            if (renewed.IsSuccess)
            {
                _row = _row! with { ExpiresAt = renewed.Value.ExpiresAt };   // why: Renew succeeds only when the row exists
            }

            return ValueTask.FromResult(renewed);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Release(LeaseGrant grant, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!LeaseDecision.Holds(_row, grant))
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.LeaseLost.With(string.Create(CultureInfo.InvariantCulture, $"epoch {grant.Epoch} is not held")));
            }

            _row = _row! with { ExpiresAt = clock.GetUtcNow(), Released = true };   // why: Holds is false for a null row
            return ValueTask.FromResult<Result<Unit>>(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<LeaseRow?>> Read(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<LeaseRow?>>(_row);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!LeaseDecision.Holds(_row, grant))
            {
                return ValueTask.FromResult<Result<LedgerAppend>>(SindriErrors.LeaseStale.With(string.Create(CultureInfo.InvariantCulture, $"epoch {grant.Epoch} is not current")));
            }

            var earlier = _events.Find(e => string.Equals(e.IdempotencyKey, entry.IdempotencyKey, StringComparison.Ordinal));
            if (earlier is not null)
            {
                return ValueTask.FromResult<Result<LedgerAppend>>(new LedgerAppend(earlier.Sequence, AlreadyRecorded: true));
            }

            var recorded = new LedgerEvent(_events.Count + 1, grant.Epoch, entry.Envelope, entry.IdempotencyKey, entry.Item);
            _events.Add(recorded);
            return ValueTask.FromResult<Result<LedgerAppend>>(new LedgerAppend(recorded.Sequence, AlreadyRecorded: false));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(
                _events.Where(e => e.Sequence > sequence).Take(Math.Max(max, 1)).ToList());
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<LedgerEvent?>>(
                _events.LastOrDefault(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal)));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SchemaStatus>>(new SchemaStatus(1, 1));
}
