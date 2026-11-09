using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.PollConnections;
using Aw.Sindri.Testing.Connections;
using Aw.Sindri.Tests.Connections;

namespace Aw.Sindri.Tests.PollConnections;

/// <summary>Doctor's 'connections:catchup' rows (#2437): a capped or gapped read warns and names its connection, event kind, reason and time; a later poll of the same pair clears it.</summary>
[Lane(TestLane.Unit)]
public sealed class CatchUpDoctorChecksTests
{
    private readonly ConnectionHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<IReadOnlyList<DoctorCheck>> Rows()
    {
        var factory = new EventFactory(EventScope.ControlPlane, _h.Clock, _h.Ids, SliceHarness.Json, _h.Callers);
        return await new CatchUpDoctorChecks(_h.Ledger, factory).Run(Token);
    }

    [Fact]
    public async Task A_capped_read_warns_and_names_the_connection_kind_and_reason()
    {
        var name = ConnectionSamples.Name();
        await _h.Hold();
        await _h.Events().CatchUpStopped(name, "linear.issue.labeled", "history order", grant: null, Token);

        var row = Assert.Single(await Rows());

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains(name.ToString(), row.Detail, StringComparison.Ordinal);
        Assert.Contains("linear.issue.labeled", row.Detail, StringComparison.Ordinal);
        Assert.Contains("history order", row.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task No_capped_read_is_ok()
    {
        var row = Assert.Single(await Rows());

        Assert.Equal(CheckState.Ok, row.State);
    }

    [Fact]
    public async Task A_later_poll_of_the_same_pair_clears_the_warning()
    {
        var name = ConnectionSamples.Name();
        await _h.Hold();
        await _h.Events().CatchUpStopped(name, "linear.issue.labeled", "history order", grant: null, Token);
        await _h.Events().Polled(new PollCounts(name, "linear.issue.labeled", 1, 1, 0), grant: null, Token);

        var row = Assert.Single(await Rows());

        Assert.Equal(CheckState.Ok, row.State);
    }

    // why: CatchUp.Record writes a pass's polled row BEFORE its capped row; that pass's own poll must not clear its own cap
    [Fact]
    public async Task A_poll_written_before_the_cap_in_the_same_pass_still_warns()
    {
        var name = ConnectionSamples.Name();
        await _h.Hold();
        await _h.Events().Polled(new PollCounts(name, "linear.issue.labeled", 1, 1, 0), grant: null, Token);
        await _h.Events().CatchUpStopped(name, "linear.issue.labeled", "history order", grant: null, Token);

        var row = Assert.Single(await Rows());

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("history order", row.Detail, StringComparison.Ordinal);
    }

    // why: #2480 (a quiet pair per kind, since a fixed clock makes one key per pair): the doctor ran on every `sindri doctor`; it paged the whole ledger. It must read the rare capped rows plus one lookup per capped pair, not every poll row.
    [Fact]
    public async Task A_big_ledger_of_polls_is_not_read_row_by_row()
    {
        var capped = ConnectionSamples.Name();
        var quiet = ConnectionSamples.Name();
        await _h.Hold();
        for (var i = 0; i < 1_200; i++)
        {
            await _h.Events().Polled(new PollCounts(quiet, "kind." + i.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, 0, 0), grant: null, Token);
        }

        await _h.Events().CatchUpStopped(capped, "linear.issue.labeled", "history order", grant: null, Token);
        var counting = new RowCountingReader(_h.Ledger);
        var factory = new EventFactory(EventScope.ControlPlane, _h.Clock, _h.Ids, SliceHarness.Json, _h.Callers);

        var row = Assert.Single(await new CatchUpDoctorChecks(counting, factory).Run(Token));

        Assert.Equal(CheckState.Warn, row.State);
        Assert.Contains("history order", row.Detail, StringComparison.Ordinal);
        Assert.True(counting.Rows < 50, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the doctor read {counting.Rows} rows of a 1201-row ledger"));
    }

    private sealed class RowCountingReader(ILedgerReader inner) : ILedgerReader
    {
        public int Rows { get; private set; }

        public async ValueTask<Result<IReadOnlyList<LedgerEvent>>> ReadAfter(long sequence, int max, CancellationToken cancellationToken)
        {
            var rows = await inner.ReadAfter(sequence, max, cancellationToken);
            Rows += rows.IsError ? 0 : rows.Value.Count;
            return rows;
        }

        public async ValueTask<Result<LedgerScan>> Scan(long sequence, int max, LedgerFilter filter, CancellationToken cancellationToken)
        {
            var scan = await inner.Scan(sequence, max, filter, cancellationToken);
            Rows += scan.IsError ? 0 : scan.Value.Events.Count;
            return scan;
        }

        public ValueTask<Result<LedgerEvent?>> Latest(string type, CancellationToken cancellationToken) => inner.Latest(type, cancellationToken);

        public ValueTask<Result<LedgerEvent?>> ByKey(string idempotencyKey, CancellationToken cancellationToken) => inner.ByKey(idempotencyKey, cancellationToken);

        public ValueTask<Result<long>> Head(CancellationToken cancellationToken) => inner.Head(cancellationToken);
    }
}
