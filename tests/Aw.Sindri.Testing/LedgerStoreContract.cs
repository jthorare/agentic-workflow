using System.Globalization;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ILedgerStore"/> honours (factory addendum §17.20–17.21): appends are fenced by the
/// lease epoch, idempotent by key, and read back in order with the whole envelope.
/// </summary>
public abstract class LedgerStoreContract
{
    private readonly SequentialIdGenerator _ids = new();

    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>The probe; this process is pid 100.</summary>
    protected FakeProcessProbe Probe { get; } = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The stores over a fresh, empty file or memory.</summary>
    /// <param name="clock">The only clock.</param>
    /// <param name="probe">The process probe.</param>
    /// <returns>The stores.</returns>
    protected abstract ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe);

    /// <summary>An appended event reads back with every envelope field, its epoch and its key.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_appended_event_reads_back_with_its_envelope()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var entry = Entry("process.started", "k1", item: "ITEM-1", trace: "trace-1");

        var appended = (await ledger.Append(entry, grant, Token)).Value;
        var read = (await ledger.ReadAfter(0, 10, Token)).Value;

        Expect.True(!appended.AlreadyRecorded);
        var only = Expect.Only(read);
        Expect.Equal(new LedgerEvent(appended.Sequence, grant.Epoch, entry.Envelope, "k1", "ITEM-1"), only);
    }

    /// <summary>A replayed key is reported as already recorded, with the first event's position, and writes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_replayed_key_is_recorded_once()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var first = (await ledger.Append(Entry("service.heartbeat", "same"), grant, Token)).Value;

        var again = (await ledger.Append(Entry("service.heartbeat", "same"), grant, Token)).Value;

        Expect.Equal(new LedgerAppend(first.Sequence, AlreadyRecorded: true), again);
        _ = Expect.Only((await ledger.ReadAfter(0, 10, Token)).Value);
    }

    /// <summary>An append under an epoch that isn't current is refused and writes nothing (fencing).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_append_with_a_stale_epoch_is_refused()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var old = (await lease.Acquire(Probe.Self(), Token)).Value;
        _ = await lease.Release(old, Token);
        _ = (await lease.Acquire(Probe.Self(), Token)).Value;

        var refused = await ledger.Append(Entry("process.started", "k"), old, Token);

        Expect.Equal(SindriErrors.LeaseStale.Code, refused.Error.Code);
        Expect.Equal(0, (await ledger.ReadAfter(0, 10, Token)).Value.Count);
    }

    /// <summary>ReadAfter returns events after the cursor, oldest first, at most max; Latest finds the newest of a type.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Reads_are_ordered_bounded_and_latest_finds_the_newest()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        for (var i = 1; i <= 5; i++)
        {
            _ = await ledger.Append(Entry(i % 2 == 0 ? "service.heartbeat" : "other", string.Create(CultureInfo.InvariantCulture, $"k{i}")), grant, Token);
        }

        var page = (await ledger.ReadAfter(2, 2, Token)).Value;
        var latest = (await ledger.Latest("service.heartbeat", Token)).Value;

        Expect.Sequence(["k3", "k4"], page.Select(e => e.IdempotencyKey));
        Expect.Equal("k4", latest?.IdempotencyKey);
        Expect.True((await ledger.Latest("never", Token)).Value is null);
    }

    /// <summary>Once anything is written, the store reports its schema as current.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_written_store_has_a_current_schema()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        _ = (await lease.Acquire(Probe.Self(), Token)).Value;
        Expect.True((await ledger.Schema(Token)).Value.IsCurrent);
    }

    private LedgerEntry Entry(string type, string key, string? item = null, string? trace = null)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId(), trace).Value;
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        return new LedgerEntry(context.Envelope(type, """{"n":1}""", Clock, _ids), key, item);
    }
}
