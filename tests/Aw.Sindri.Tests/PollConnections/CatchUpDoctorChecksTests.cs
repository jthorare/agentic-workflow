using Aw.Kernel.Testing;
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
}
