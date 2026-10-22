using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Aw.Sindri.Testing;

/// <summary>
/// The contract every <see cref="ILedgerReader"/> honours (API spec C9): it reads back what the ledger's writer
/// recorded, in order with the whole envelope, and on a ledger that was never written it is empty and creates nothing.
/// </summary>
public abstract class LedgerReaderContract
{
    private readonly SequentialIdGenerator _ids = new();

    /// <summary>The contract's clock.</summary>
    protected FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    /// <summary>The probe; this process is pid 100.</summary>
    protected FakeProcessProbe Probe { get; } = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The writer's stores over a fresh, empty file or memory.</summary>
    /// <param name="clock">The only clock.</param>
    /// <param name="probe">The process probe.</param>
    /// <returns>The stores.</returns>
    protected abstract ValueTask<SindriStores> Create(TimeProvider clock, IProcessProbe probe);

    /// <summary>A reader over the same file or memory as the last <see cref="Create"/>.</summary>
    /// <returns>The reader.</returns>
    protected abstract ILedgerReader Reader();

    /// <summary>True when the ledger's backing store (its file) exists; an in-memory ledger has none to create.</summary>
    /// <returns>Whether it exists.</returns>
    protected abstract bool LedgerExists();

    /// <summary>A reader sees every event the writer recorded, oldest first, the newest of a type, and the head.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_reader_sees_what_the_store_wrote()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var first = Entry("process.started", "k1");
        _ = await ledger.Append(first, grant, Token);
        _ = await ledger.Append(Entry("service.heartbeat", "k2"), grant, Token);
        var last = (await ledger.Append(Entry("service.heartbeat", "k3"), grant, Token)).Value;
        var reader = Reader();

        var all = (await reader.ReadAfter(0, 10, Token)).Value;

        Expect.Sequence(["k1", "k2", "k3"], all.Select(e => e.IdempotencyKey));
        Expect.Equal(first.Envelope, all[0].Envelope);
        Expect.Equal("k3", (await reader.Latest("service.heartbeat", Token)).Value?.IdempotencyKey);
        Expect.Equal(last.Sequence, (await reader.Head(Token)).Value);
        Expect.Sequence(["k2"], (await reader.ReadAfter(1, 1, Token)).Value.Select(e => e.IdempotencyKey));
    }

    /// <summary>On a ledger nothing was written to, a reader is empty with head 0 and creates no file.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_reader_on_a_missing_ledger_is_empty_and_creates_nothing()
    {
        _ = await Create(Clock, Probe);
        var reader = Reader();

        Expect.Equal(0, (await reader.ReadAfter(0, 10, Token)).Value.Count);
        Expect.True((await reader.Latest("process.started", Token)).Value is null);
        Expect.Equal(0L, (await reader.Head(Token)).Value);
        Expect.True(!LedgerExists());
    }

    /// <summary>A scan with a type filter keeps the types its globs match, exact or 'prefix.*', and reports the last row it looked at.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scan_filters_by_type_glob()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        _ = await ledger.Append(Entry("process.started", "k1"), grant, Token);
        _ = await ledger.Append(Entry("service.heartbeat", "k2"), grant, Token);
        _ = await ledger.Append(Entry("command.handled", "k3"), grant, Token);
        _ = await ledger.Append(Entry("service.ran", "k4"), grant, Token);
        var filter = new LedgerFilter(TypeFilter.Parse("service.*,process.started").Value, Item: null);

        var scan = (await Reader().Scan(0, 10, filter, Token)).Value;

        Expect.Sequence(["k1", "k2", "k4"], scan.Events.Select(e => e.IdempotencyKey));
        Expect.Equal(4L, scan.ScannedTo);
    }

    /// <summary>A scan with an item keeps only that item's events.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scan_filters_by_item()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        var mine = ItemId.New(_ids);
        _ = await ledger.Append(Entry("item.created", "k1", mine.Value), grant, Token);
        _ = await ledger.Append(Entry("item.created", "k2", ItemId.New(_ids).Value), grant, Token);
        _ = await ledger.Append(Entry("service.heartbeat", "k3"), grant, Token);
        _ = await ledger.Append(Entry("item.state-changed", "k4", mine.Value), grant, Token);

        var scan = (await Reader().Scan(0, 10, new LedgerFilter(Types: null, mine), Token)).Value;

        Expect.Sequence(["k1", "k4"], scan.Events.Select(e => e.IdempotencyKey));
        Expect.True(scan.Events.All(e => e.Item == mine.Value));
    }

    /// <summary>A scan with an instance keeps the rows that instance mirrored (not its children's or another's), looks at the whole ledger whatever the scan limit, and ends at the head.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scan_filters_by_instance_and_ignores_the_scan_limit()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        _ = await ledger.Append(Entry("workflow.started", "workflow:T-1/step/1:run-a:1"), grant, Token);
        _ = await ledger.Append(Entry("workflow.started", "workflow:T-1/step/1/step.2:run-b:1"), grant, Token);
        _ = await ledger.Append(Entry("workflow.started", "workflow:T-2/step/1:run-c:1"), grant, Token);
        _ = await ledger.Append(Entry("service.heartbeat", "workflow-not:T-1/step/1:x"), grant, Token);
        _ = await ledger.Append(Entry("workflow.transition", "workflow:T-1/step/1:run-a:3"), grant, Token);
        var filter = new LedgerFilter(Types: null, Item: null, ScanLimit: 2, Instance: InstanceAddress.From("T-1/step/1"));

        var scan = (await Reader().Scan(0, 10, filter, Token)).Value;
        var after = (await Reader().Scan(1, 10, filter, Token)).Value;

        Expect.Sequence(["workflow:T-1/step/1:run-a:1", "workflow:T-1/step/1:run-a:3"], scan.Events.Select(e => e.IdempotencyKey));
        Expect.Equal(5L, scan.ScannedTo);
        Expect.Sequence(["workflow:T-1/step/1:run-a:3"], after.Events.Select(e => e.IdempotencyKey));
    }

    /// <summary>A scan looks at no more than its scan limit of rows past the cursor and says how far it got, so a sparse filter pages on.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scan_stops_at_its_scan_limit_and_reports_ScannedTo()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        for (var i = 1; i <= 5; i++)
        {
            _ = await ledger.Append(Entry("service.heartbeat", "h" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)), grant, Token);
        }

        _ = await ledger.Append(Entry("process.stopped", "k6"), grant, Token);
        var filter = new LedgerFilter(TypeFilter.Parse("process.stopped").Value, Item: null, ScanLimit: 3);
        var reader = Reader();

        var first = (await reader.Scan(0, 10, filter, Token)).Value;
        var second = (await reader.Scan(first.ScannedTo, 10, filter, Token)).Value;

        Expect.Equal(0, first.Events.Count);
        Expect.Equal(3L, first.ScannedTo);
        Expect.Sequence(["k6"], second.Events.Select(e => e.IdempotencyKey));
        Expect.Equal(6L, second.ScannedTo);
    }

    /// <summary>A page that fills ends at its last row, so the next call resumes right after it; an exact type matches no longer type.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scan_ends_a_full_page_at_its_last_row()
    {
        var (lease, ledger) = await Create(Clock, Probe);
        var grant = (await lease.Acquire(Probe.Self(), Token)).Value;
        _ = await ledger.Append(Entry("service.ran", "k1"), grant, Token);
        _ = await ledger.Append(Entry("service.ran-late", "k2"), grant, Token);
        _ = await ledger.Append(Entry("service.ran", "k3"), grant, Token);
        _ = await ledger.Append(Entry("service.ran", "k4"), grant, Token);
        var filter = new LedgerFilter(TypeFilter.Parse("service.ran").Value, Item: null);
        var reader = Reader();

        var first = (await reader.Scan(0, 2, filter, Token)).Value;
        var second = (await reader.Scan(first.ScannedTo, 2, filter, Token)).Value;

        Expect.Sequence(["k1", "k3"], first.Events.Select(e => e.IdempotencyKey));
        Expect.Equal(3L, first.ScannedTo);
        Expect.Sequence(["k4"], second.Events.Select(e => e.IdempotencyKey));
        Expect.Equal(4L, second.ScannedTo);
    }

    /// <summary>On a ledger nothing was written to, a scan is empty at sequence 0 and creates no file.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Scan_on_a_missing_ledger_is_empty()
    {
        _ = await Create(Clock, Probe);

        var scan = (await Reader().Scan(0, 10, LedgerFilter.All, Token)).Value;

        Expect.Equal(0, scan.Events.Count);
        Expect.Equal(0L, scan.ScannedTo);
        Expect.True(!LedgerExists());
    }

    private LedgerEntry Entry(string type, string key, string? item = null)
    {
        var context = EventContext.Create("host:sindri", "local", 0, _ids.NewId()).Value;
        Clock.Advance(TimeSpan.FromMilliseconds(1));
        return new LedgerEntry(context.Envelope(type, """{"n":1}""", Clock, _ids), key, item);
    }
}
