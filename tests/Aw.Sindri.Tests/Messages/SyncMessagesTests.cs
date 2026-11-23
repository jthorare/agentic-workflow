using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.SyncMessages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// M1e (messages spec §5.2, §5.3, W10, W16): the 'messages-sync' service on the poll path, over FakeSlackServer and a FakeTimeProvider. The wall-clock
/// lines of W16 run in the perf lane (M3f); these check the logic.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SyncMessagesTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public void Sync_service_is_named_messages_sync() =>
        Assert.Equal("messages-sync", new SyncMessagesService().Identity().Value);

    [Fact]
    public Task Gap_without_marker_fails() => RedUntil.Phase("M1e", async () =>
    {
        // M1e: the fake answers history with a hole (has_more past the cursor); the conversation window must carry a GapMarker for it
        var ran = await Run();

        Assert.True(ran.IsSuccess);
    });

    [Fact]
    public Task Message_appears_within_30s_on_the_poll_path() => RedUntil.Phase("M1e", async () =>
    {
        // M1e: a DM lands on the fake at t; with the clock at t + 30 s the poll has stored it and Needs reply holds its row
        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
    });

    [Fact]
    public Task Reply_in_an_old_thread_is_seen() => RedUntil.Phase("M1e", async () =>
    {
        // M1e: a reply lands in a thread whose root is past the hot set; the thread-reply check (20% of the bucket) finds it
        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
    });

    [Fact]
    public Task Cold_start_fill_gives_a_sample() => RedUntil.Phase("M1e", async () =>
    {
        // M1e: a fresh store fills from search.messages: rows are 'partial' until their thread is fetched
        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
    });

    private static async Task<Kernel.Result<ScheduledResult>> Run()
    {
        var harness = new SliceHarness();
        var grant = await harness.HoldLease();
        return await new SyncMessagesService().Run(new ScheduledRun(grant, harness.Clock.GetUtcNow()), Cancel);
    }
}
