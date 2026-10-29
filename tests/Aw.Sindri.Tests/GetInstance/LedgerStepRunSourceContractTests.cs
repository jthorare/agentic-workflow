using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.GetInstance;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Ledger;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>The ledger-backed step-run source keeps the contract over a real SQLite ledger, and reads past one page of rows.</summary>
[Lane(TestLane.Integration)]
public sealed class LedgerStepRunSourceContractTests : StepRunSourceContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private LedgerDatabase? _readerDatabase;
    private LedgerDatabase? _writerDatabase;

    public void Dispose()
    {
        _readerDatabase?.Dispose();
        _writerDatabase?.Dispose();
        _home.Dispose();
    }

    protected override async ValueTask<StepRunRead> Create(IReadOnlyList<(InstanceAddress Address, StepRunRow Row)> rows)
    {
        var stores = SqliteStores.Create(_home.Path, _h.Clock, _h.Probe);
        var grant = (await stores.Lease.Acquire(_h.Probe.Self(), TestContext.Current.CancellationToken)).Value;
        var entries = rows.Select((row, i) => _h.Events.Entry(row.Row.Type, row.Row.Payload, string.Create(CultureInfo.InvariantCulture, $"workflow:{row.Address.Value}:run:{i}")) with { Item = row.Address.Value[..row.Address.Value.IndexOf('/', StringComparison.Ordinal)] });
        var writer = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _h.Clock);   // the rows go in one write session (BulkLedger), not one per row
        _writerDatabase = writer;
        _ = await BulkLedger.Append(writer, grant, entries, TestContext.Current.CancellationToken);

        _readerDatabase = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _h.Clock);
        return new LedgerStepRunSource(new SqliteLedgerReader(_readerDatabase), _h.Events).EventsOf;
    }

    protected override ValueTask<StepRunRead> CreateFailing() => ValueTask.FromResult<StepRunRead>(new LedgerStepRunSource(new UnreadableLedger(), _h.Events).EventsOf);

    [Fact]
    public async Task A_row_at_each_page_edge_of_a_long_ledger_is_kept()
    {
        var rows = new List<(InstanceAddress, StepRunRow)>();
        for (var i = 0; i < 1_001; i++)
        {
            var edge = i is 0 or 499 or 500 or 1_000;
            rows.Add((edge ? Mine : Other, StepRunFixtures.Started(string.Create(CultureInfo.InvariantCulture, $"s{i}"), "1.1")));
        }

        var read = await Create(rows);

        var events = await read(Mine, TestContext.Current.CancellationToken);

        Assert.Equal("s0 s499 s500 s1000", string.Join(' ', events.Value.Select(static e => e.Frame?.State)));
    }

    [Fact]
    public async Task The_read_touches_only_the_instances_rows()
    {
        var rows = new List<(InstanceAddress, StepRunRow)>();
        for (var i = 0; i < 700; i++)
        {
            rows.Add((i is 3 or 650 ? Mine : Other, StepRunFixtures.Started(string.Create(CultureInfo.InvariantCulture, $"s{i}"), "1.1")));
        }

        _ = await Create(rows);
        var counting = new RecordingLedgerReader(new SqliteLedgerReader(_readerDatabase!));

        var events = await new LedgerStepRunSource(counting, _h.Events).EventsOf(Mine, TestContext.Current.CancellationToken);

        Assert.Equal("s3 s650", string.Join(' ', events.Value.Select(static e => e.Frame?.State)));
        Assert.Equal(2, counting.RowsRead);
    }

    private sealed class UnreadableLedger : ILedgerReader
    {
        private static readonly Error _failure = Core.SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full");

        public ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<IReadOnlyList<LedgerEvent>>(_failure));

        public ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<LedgerScan>(_failure));

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<LedgerEvent?>(_failure));

        public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<LedgerEvent?>(_failure));

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Failure<long>(_failure));
    }
}
