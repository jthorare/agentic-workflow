using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Health;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands.Chain;

/// <summary>Before the projector's first publish the status is Starting, not Rebuilding: it isn't Healthy, but a command that reads views still waits for the pass instead of being refused (#2053).</summary>
public sealed class AwaitingStartingViewsTests : IAsyncDisposable
{
    private readonly ChainHarness _c = new(fixtureReadsViews: true);

    public ValueTask DisposeAsync() => _c.DisposeAsync();

    private static CallerContext Human => A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

    [Fact, Trait("Lane", "unit")]
    public void A_fresh_progress_reads_starting_not_rebuilding_and_not_healthy()
    {
        var views = _c.Progress.Current;

        Assert.True(views is { Starting: true, Rebuilding: false, Stalled: false });
        Assert.NotEqual(HealthWord.Healthy, HealthSummary.For(running: true, views, [], ProfileApproval.Approved).Word);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_command_that_reads_views_before_the_first_publish_waits_and_runs_once_it_lands()
    {
        _ = await _c.H.HoldLease();
        Assert.True((await _c.Run<FixtureCommand, CommandAccepted>(new FixtureCommand("accept"), Human)).IsSuccess);   // head 0 needs no wait: this moves the ledger on
        var head = (await _c.Ledger.Head(TestContext.Current.CancellationToken)).Value;
        Assert.True(head > 0);

        var running = _c.Start(new FixtureCommand("accept"), Human);
        _c.H.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(running.IsCompleted);   // waiting, not refused with SND-API-010
        _c.Progress.Publish(new ViewsStatus(head, head, _c.H.Clock.GetUtcNow(), Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null));

        Assert.True((await running).IsSuccess);
        Assert.False(_c.Progress.Current.Starting);
    }
}
