using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;
using static Aw.Sindri.Tests.Sessions.SessionData;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>
/// An overrun pass keeps the last list with its old time (#4244). One or two in a row keep the plain overrun row. From the third in a row the row says how old
/// the shown list is, or that there is no list yet. The Agents card and the Agents page show this row's text as it is served.
/// </summary>
public sealed class CollectorStaleListTests
{
    private static readonly TimeSpan _budget = TimeSpan.FromSeconds(1);

    private static async Task<SessionProblem> Overruns(CollectorHarness c, StuckTable table, int count, TimeSpan before)
    {
        table.BlockReads = true;
        var started = table.Reads;
        var first = c.Pass(_budget);
        await table.Tables.Reached(started + 1);
        c.Base.Clock.Advance(_budget);
        Assert.True((await first).Overran);
        c.Base.Clock.Advance(before);
        for (var n = 2; n <= count; n++)
        {
            Assert.True((await c.Pass(_budget)).Overran);   // the stuck read is not started again, so these return at once
        }

        return Assert.Single(c.Snapshot.Current!.Problems, static p => p.IsOverrun);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Two_overruns_in_a_row_keep_the_plain_overrun_row()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        _ = await c.Pass();

        var row = await Overruns(c, table, 2, TimeSpan.FromMinutes(2));
        table.Release();

        Assert.Equal("A pass took longer than its 500 ms budget", row.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_overruns_in_a_row_say_how_old_the_shown_list_is()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = fake.With(Proc(10, 1, "claude", argv0: "claude"), cwd: "/w/api");
        _ = await c.Pass();
        var shownAt = c.Now;

        var row = await Overruns(c, table, 3, TimeSpan.FromMinutes(2));
        table.Release();

        Assert.Equal(("collector", "SND-SESSION-001"), (row.Source, row.Code));
        Assert.Equal("The agent list is from 2 min ago: the last 3 passes took longer than their 500 ms budget", row.Text);
        Assert.Equal(shownAt, c.Snapshot.Current!.ObservedAt);
        _ = Assert.Single(c.Snapshot.Current.Sessions);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_list_under_a_minute_old_is_aged_in_seconds()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = await c.Pass();

        var row = await Overruns(c, table, 4, TimeSpan.FromSeconds(44));
        table.Release();

        Assert.Equal("The agent list is from 45 s ago: the last 4 passes took longer than their 500 ms budget", row.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Three_overruns_before_any_list_say_there_is_no_list_yet()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);

        var row = await Overruns(c, table, 3, TimeSpan.FromSeconds(5));
        table.Release();

        Assert.Equal("No agent list yet: the last 3 passes took longer than their 500 ms budget", row.Text);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_good_pass_starts_the_count_again()
    {
        var fake = new FakeProcessTable();
        using var table = new StuckTable(fake);
        var c = new CollectorHarness(processes: table);
        _ = await c.Pass();
        _ = await Overruns(c, table, 3, TimeSpan.FromMinutes(2));
        table.BlockReads = false;
        table.Release();
        await c.Discovery.AbandonedSettled();
        Assert.True((await c.Pass()).Good);
        table.Rearm();

        var row = await Overruns(c, table, 1, TimeSpan.Zero);
        table.Release();

        Assert.Equal("A pass took longer than its 500 ms budget", row.Text);
    }
}
