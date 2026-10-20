using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;
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

    public void Dispose()
    {
        _readerDatabase?.Dispose();
        _home.Dispose();
    }

    protected override async ValueTask<StepRunRead> Create(IReadOnlyList<(InstanceAddress Address, RecordedWorkflowEvent Event)> rows)
    {
        var stores = SqliteStores.Create(_home.Path, _h.Clock, _h.Probe);
        var grant = (await stores.Lease.Acquire(_h.Probe.Self(), TestContext.Current.CancellationToken)).Value;
        for (var i = 0; i < rows.Count; i++)
        {
            var (address, recorded) = rows[i];
            var entry = _h.Events.Entry(recorded.Type, recorded.Payload with { Instance = recorded.Payload.Instance }, string.Create(CultureInfo.InvariantCulture, $"workflow:{address.Value}:run:{i}")) with { Item = address.Value[..address.Value.IndexOf('/', StringComparison.Ordinal)] };
            Assert.False((await stores.Ledger.Append(entry, grant, TestContext.Current.CancellationToken)).IsError);
        }

        _readerDatabase = new LedgerDatabase(new SindriSettings(_home.Path, Path.Combine(_home.Path, "profile.yaml")), _h.Clock);
        return new LedgerStepRunSource(new SqliteLedgerReader(_readerDatabase), _h.Events).EventsOf;
    }

    protected override ValueTask<StepRunRead> CreateFailing() => ValueTask.FromResult<StepRunRead>(new LedgerStepRunSource(new UnreadableLedger(), _h.Events).EventsOf);

    [Fact]
    public async Task A_row_at_each_page_edge_of_a_long_ledger_is_kept()
    {
        var rows = new List<(InstanceAddress, RecordedWorkflowEvent)>();
        for (var i = 0; i < 1_001; i++)
        {
            var edge = i is 0 or 499 or 500 or 1_000;
            rows.Add((edge ? Mine : Other, StepRunFixtures.Started(string.Create(CultureInfo.InvariantCulture, $"s{i}"), "1.1")));
        }

        var read = await Create(rows);

        var events = await read(Mine, TestContext.Current.CancellationToken);

        Assert.Equal("s0 s499 s500 s1000", string.Join(' ', events.Value.Select(static e => e.Payload.To)));
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

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Failure<long>(_failure));
    }
}
