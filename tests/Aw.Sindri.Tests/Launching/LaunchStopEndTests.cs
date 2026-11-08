using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.SessionReports;

namespace Aw.Sindri.Tests.Launching;

/// <summary>Stopping a launch ends its session as an exit: the row stops being live, nothing is recorded twice and the sweep never calls it lost.</summary>
[Lane(TestLane.Unit)]
public sealed class LaunchStopEndTests
{
    private readonly SessionHarness _h = new();
    private long _synced;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private LaunchStopEnd Ender() => new(_h.Events(), new LaunchViews(_h.Views.Views));

    private async Task Sync()
    {
        var page = (await _h.Views.Slice.Store.ReadAfter(_synced, 1000, Token)).Value;
        if (page.Count > 0)
        {
            await _h.Views.Apply(page);
            _synced = page[^1].Sequence;
        }
    }

    private IReadOnlyList<SessionEnded> Ended() =>
        [.. _h.Views.Slice.Store.Events.Where(e => string.Equals(e.Envelope.Type, EventTypes.SessionEnded, StringComparison.Ordinal)).Select(e => _h.Views.Slice.Events.Payload<SessionEnded>(e).Value)];

    private async Task<LaunchRow> Row() => (await _h.Views.Views.Get<LaunchRow>(ViewNames.Launches, A.FirstLaunchId.Value, Token)).Value!;

    [Fact]
    public async Task Stop_records_session_ended_with_exit()
    {
        await _h.Started();
        await Sync();

        await Ender().EndIfLive(A.FirstLaunchId, Token);
        await Sync();

        var ended = Assert.Single(Ended());
        Expect.Equal((SessionEndReason.Exit, A.FirstLaunchId), (ended.Reason, ended.Launch));
        Assert.False((await Row()).Live);
        Assert.DoesNotContain(A.FirstLaunchId, _h.Liveness.Active());
    }

    [Fact]
    public async Task Stop_after_end_records_nothing_more()
    {
        await _h.Started();
        await Sync();
        await Ender().EndIfLive(A.FirstLaunchId, Token);
        await Sync();

        await Ender().EndIfLive(A.FirstLaunchId, Token);
        await Sync();

        _ = Assert.Single(Ended());
    }

    [Fact]
    public async Task Stop_after_the_sweep_ended_it_records_nothing_more()
    {
        await _h.Started();
        await Sync();
        _ = await _h.Events().End(A.FirstLaunchId, SessionEndReason.Lost, exitCode: null, Token);
        await Sync();

        await Ender().EndIfLive(A.FirstLaunchId, Token);
        await Sync();

        Expect.Equal(SessionEndReason.Lost, Assert.Single(Ended()).Reason);
    }

    [Fact]
    public async Task Stop_of_a_launch_with_no_live_row_records_nothing()
    {
        await _h.Started();
        await Sync();

        await Ender().EndIfLive(LaunchId.From("LCH-" + new string('0', 31) + "9"), Token);
        await Sync();

        Assert.Empty(Ended());
    }
}
