using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RetainMessages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// M1c (messages spec §8.1): the nightly 'messages-retention' pass deletes messages past 30 days, but skips a thread in Needs reply, with a live snooze or
/// with owner state; snooze is read from the ledger's home.row.snoozed and .woke (seeded events), so a views rebuild cannot unsnooze it; any skip ends at 180 days.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RetainMessagesTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public Task Retention_keeps_a_thread_in_needs_reply() => RedUntil.Phase("M1c", async () =>
    {
            // M1c seeds a 35-day-old unanswered DM: it stays in Needs reply with its text
            var ran = await Run();

            Assert.True(ran.IsSuccess);
            Assert.Equal(ScheduledResult.Quiet, ran.Value);
    });

    [Fact]
    public Task Retention_keeps_a_snoozed_thread_after_views_rebuild() => RedUntil.Phase("M1c", async () =>
    {
            // M1c seeds home.row.snoozed for a 40-day-old thread, rebuilds views.db, then runs the pass: the snooze still holds the text
            var ran = await Run();

            Assert.True(ran.IsSuccess);
            Assert.Equal(ScheduledResult.Quiet, ran.Value);
    });

    [Fact]
    public Task Retention_ends_a_skip_after_180_days() => RedUntil.Phase("M1c", async () =>
    {
            // M1c seeds a thread with a Later flag and messages 181 days old: the text goes and the row shows "text expired"
            var ran = await Run();

            Assert.True(ran.IsSuccess);
            Assert.Equal(ScheduledResult.Worked, ran.Value);
    });

    private static async Task<Aw.Kernel.Result<ScheduledResult>> Run()
    {
        var harness = new SliceHarness();
        var grant = await harness.HoldLease();
        return await new RetainMessagesService().Run(new ScheduledRun(grant, harness.Clock.GetUtcNow()), Cancel);
    }
}
