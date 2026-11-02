using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Testing;

/// <summary>
/// The in-memory lease and ledger, for unit tests; passes <see cref="LeaseContract"/>, <see cref="LedgerStoreContract"/>
/// and, as its own reader, <see cref="LedgerReaderContract"/>.
/// </summary>
/// <param name="clock">The clock leases expire by.</param>
/// <param name="probe">What the store may learn about a holder's process.</param>
public sealed class InMemorySindriStore(TimeProvider clock, IProcessProbe probe) : ILease, ILedgerStore, ILedgerReader
{
    private readonly Lock _gate = new();
    private readonly List<LedgerEvent> _events = [];
    private readonly ILedgerSignal? _signal;
    private LeaseRow? _row;

    /// <summary>A store that pulses <paramref name="signal"/> after each new append, as the SQLite ledger does after its commit.</summary>
    /// <param name="clock">The clock leases expire by.</param>
    /// <param name="probe">What the store may learn about a holder's process.</param>
    /// <param name="signal">The signal to pulse.</param>
    public InMemorySindriStore(TimeProvider clock, IProcessProbe probe, ILedgerSignal signal)
        : this(clock, probe)
    {
        _signal = signal;
    }

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

    /// <summary>Runs at the start of every <see cref="Append"/>, before its token is checked (a test aborts its request here).</summary>
    public Action<LedgerEntry>? BeforeAppend { get; set; }

    /// <summary>When set and it answers an error for an entry, <see cref="Append"/> refuses that entry with it (a write that fails while the lease is still held).</summary>
    public Func<LedgerEntry, Error?>? FailAppend { get; set; }

    /// <summary>When set, <see cref="Append"/> throws it (a ledger that fails in a way the contract doesn't model).</summary>
    public Exception? AppendFailure { get; set; }

    /// <inheritdoc/>
    public ValueTask<Result<LedgerAppend>> Append(LedgerEntry entry, LeaseGrant grant, CancellationToken cancellationToken)
    {
        BeforeAppend?.Invoke(entry);
        cancellationToken.ThrowIfCancellationRequested();   // the real ledger passes the token to its connection
        if (FailAppend?.Invoke(entry) is { } refused)
        {
            return ValueTask.FromResult<Result<LedgerAppend>>(refused);
        }

        if (AppendFailure is { } failure)
        {
            throw failure;
        }

        var appended = Record(entry, grant);
        if (appended is { IsSuccess: true, Value.AlreadyRecorded: false })
        {
            _signal?.Pulse(appended.Value.Sequence);   // after the "commit", outside the gate, never on a replay
        }

        return ValueTask.FromResult(appended);
    }

    /// <inheritdoc/>
    public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        lock (_gate)
        {
            var upper = filter.UpperFrom(sequence);
            var kept = _events.Where(e => e.Sequence > sequence && e.Sequence <= upper && filter.Keeps(e)).Take(Math.Max(max, 1)).ToList();
            var head = _events.Count == 0 ? 0 : _events[^1].Sequence;
            return ValueTask.FromResult<Result<LedgerScan>>(filter.Scanned(sequence, head, max, kept));
        }
    }

    /// <inheritdoc cref="ILedgerStore.ReadAfter"/>
    public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<LedgerEvent>>>(
                _events.Where(e => e.Sequence > sequence).Take(Math.Max(max, 1)).ToList());
        }
    }

    /// <inheritdoc cref="ILedgerStore.Latest"/>
    public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<LedgerEvent?>>(
                _events.LastOrDefault(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal)));
        }
    }

    /// <inheritdoc cref="ILedgerReader.ByKey"/>
    public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<LedgerEvent?>>(_events.FirstOrDefault(e => string.Equals(e.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)));
        }
    }

    /// <inheritdoc cref="ILedgerStore.Head"/>
    public ValueTask<Result<long>> Head(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<long>>(_events.Count == 0 ? 0 : _events[^1].Sequence);
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<SchemaStatus>> Schema(CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SchemaStatus>>(new SchemaStatus(1, 1));

    private Result<LedgerAppend> Record(LedgerEntry entry, LeaseGrant grant)
    {
        lock (_gate)
        {
            if (!LeaseDecision.Holds(_row, grant))
            {
                return SindriErrors.LeaseStale.With(string.Create(CultureInfo.InvariantCulture, $"epoch {grant.Epoch} is not current"));
            }

            var earlier = _events.Find(e => string.Equals(e.IdempotencyKey, entry.IdempotencyKey, StringComparison.Ordinal));
            if (earlier is not null)
            {
                return new LedgerAppend(earlier.Sequence, AlreadyRecorded: true);
            }

            var recorded = new LedgerEvent(_events.Count + 1, grant.Epoch, entry.Envelope, entry.IdempotencyKey, entry.Item);
            _events.Add(recorded);
            return new LedgerAppend(recorded.Sequence, AlreadyRecorded: false);
        }
    }
}
