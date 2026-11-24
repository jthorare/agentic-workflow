using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.RetainMessages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// M1c (messages spec §8.1): the nightly 'messages-retention' pass deletes messages past 30 days, but skips a thread in Needs reply, with a live snooze or
/// with owner state; snooze is read from the ledger's home.row.snoozed and .woke (seeded events), so a views rebuild cannot unsnooze it; any skip ends at 180 days.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class RetainMessagesTests : IDisposable
{
    private const string Harbor = "C0HARBOR";

    private readonly SliceHarness _harness = new();
    private readonly MessagesHome _home;
    private LeaseGrant? _grant;
    private int _events;

    public RetainMessagesTests()
    {
        _home = new MessagesHome(_harness.Clock);
    }

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private DateTimeOffset Now => _harness.Clock.GetUtcNow();

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task Retention_keeps_a_thread_in_needs_reply()
    {
        // a 35-day-old unanswered DM: it stays in Needs reply with its text
        await _home.Conversation("T0HARBOR", "D0SAM", ConversationKind.Dm);
        _ = await _home.Put(new StoredMessage("D0SAM", MessagesHome.Ts(Now.AddDays(-35)), ThreadTs: null, "U0SAM", "can you ship the fare table today?", Mine: false, NamesMe: false));

        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Quiet, ran.Value);
        Assert.Equal(1L, _home.Count("SELECT COUNT(*) FROM messages WHERE conversation = 'D0SAM'"));
        Assert.Equal(1L, _home.Matches("fare"));
    }

    [Fact]
    public async Task Retention_keeps_a_snoozed_thread_after_views_rebuild()
    {
        // a 40-day-old thread where I spoke last (not in Needs reply), snoozed through the ledger. This home has no views.db at all: whatever a views
        // rebuild or a stalled projector does to home_rows, the pass reads only the ledger's events, so the snooze still holds the text
        var key = await SeedAnsweredThread(Now.AddDays(-40), "berth");
        await Append("home.row.snoozed", Snooze(key, Now.AddDays(7)));

        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Quiet, ran.Value);
        Assert.False(File.Exists(Path.Combine(_home.Home, "views.db")));
        Assert.Equal(2L, _home.Count($"SELECT COUNT(*) FROM messages WHERE conversation = '{Harbor}'"));
        Assert.Equal(1L, _home.Matches("berth"));
    }

    [Fact]
    public async Task Retention_takes_a_thread_once_its_snooze_woke()
    {
        var key = await SeedAnsweredThread(Now.AddDays(-40), "berth");
        await Append("home.row.snoozed", Snooze(key, Now.AddDays(7)));
        await Append("home.row.woke", $$"""{"id":"{{key}}","reason":"replied"}""");

        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
        Assert.Equal(0L, _home.Count("SELECT COUNT(*) FROM messages"));
        Assert.Equal(0L, _home.Matches("berth"));
    }

    [Fact]
    public async Task Retention_ends_a_skip_after_180_days()
    {
        // a thread with a Later flag and messages 181 days old: the text goes and the row shows "text expired"; the flag stays
        var root = MessagesHome.Ts(Now.AddDays(-181));
        _ = await SeedAnsweredThread(Now.AddDays(-181), "manifest");
        Assert.True((await _home.Store.PutThreadState(new ThreadOwnerState(Harbor, root, Later: true, Follow: false), Cancel)).IsSuccess);

        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
        Assert.Equal(0L, _home.Count("SELECT COUNT(*) FROM messages"));
        Assert.Equal(0L, _home.Matches("manifest"));
        Assert.Equal(1L, _home.Count($"SELECT COUNT(*) FROM thread_state WHERE thread_ts = '{root.Value}' AND text_expired_at IS NOT NULL AND later = 1 AND my_last_ts IS NULL"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Retention_keeps_needs_reply_when_the_cap_takes_part_of_a_kept_thread(bool mineRoot)
    {
        // the root (mine, or naming me) is 200 days old and Bob's reply 100 days: the thread waits on me, so the pass keeps it; the cap takes the root only,
        // and the thread still waits on me, with "text expired" on its row
        await _home.Conversation("T0HARBOR", Harbor, ConversationKind.Channel);
        var root = MessagesHome.Ts(Now.AddDays(-200));
        _ = await _home.Put(
            mineRoot
                ? new StoredMessage(Harbor, root, root, "U0JOI", "where is the dock?", Mine: true, NamesMe: false)
                : new StoredMessage(Harbor, root, root, "U0ADA", "<@U0JOI> where is the dock?", Mine: false, NamesMe: true),
            new StoredMessage(Harbor, MessagesHome.Ts(Now.AddDays(-100)), root, "U0BOB", "pier 9, can you check?", Mine: false, NamesMe: false));
        Assert.Equal(1L, _home.Scalar($"SELECT needs_reply FROM thread_state WHERE thread_ts = '{root.Value}'"));

        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
        Assert.Equal(1L, _home.Count($"SELECT COUNT(*) FROM messages WHERE conversation = '{Harbor}'"));   // Bob's reply, inside the cap
        Assert.Equal(1L, _home.Count($"SELECT COUNT(*) FROM thread_state WHERE thread_ts = '{root.Value}' AND needs_reply = 1 AND text_expired_at IS NOT NULL"));
    }

    [Fact]
    public async Task Retention_deletes_an_old_thread_with_its_fts_rows_and_derived_columns_and_keeps_the_rest()
    {
        // an answered thread 35 days old goes with its FTS rows and gist; a 5-day-old one in the same channel stays
        var oldRoot = MessagesHome.Ts(Now.AddDays(-35));
        _ = await SeedAnsweredThread(Now.AddDays(-35), "gangway");
        _ = await _home.Put(new StoredMessage(Harbor, MessagesHome.Ts(Now.AddDays(-5)), ThreadTs: null, "U0LEE", "tide tables", Mine: false, NamesMe: false));
        _home.Exec($"UPDATE thread_state SET gist = 'Ada asks about the gangway', gist_key = 'k1' WHERE thread_ts = '{oldRoot.Value}'");

        var ran = await Run();

        Assert.True(ran.IsSuccess);
        Assert.Equal(ScheduledResult.Worked, ran.Value);
        Assert.Equal(0L, _home.Matches("gangway"));
        Assert.Equal(1L, _home.Matches("tide"));
        Assert.Equal(1L, _home.Count($"SELECT COUNT(*) FROM thread_state WHERE thread_ts = '{oldRoot.Value}' AND gist IS NULL AND gist_key IS NULL AND my_last_ts IS NULL AND text_expired_at IS NULL"));
    }

    [Fact]
    public async Task A_snooze_event_it_cannot_read_deletes_nothing()
    {
        _ = await SeedAnsweredThread(Now.AddDays(-40), "berth");
        await Append("home.row.snoozed", """{"until":"not a time"}""");

        var ran = await Run();

        Assert.True(ran.IsError);
        Assert.Equal(2L, _home.Count("SELECT COUNT(*) FROM messages"));
    }

    // A home.row.snoozed payload: the row id and the time it wakes.
    private static string Snooze(string key, DateTimeOffset until) => $$"""{"id":"{{key}}","until":"{{until.ToString("O", CultureInfo.InvariantCulture)}}"}""";

    // Ada asks in #harbor and I answer an hour later: the thread is mine, but I spoke last, so it is not in Needs reply.
    private async Task<string> SeedAnsweredThread(DateTimeOffset at, string word)
    {
        await _home.Conversation("T0HARBOR", Harbor, ConversationKind.Channel);
        var root = MessagesHome.Ts(at);
        var keys = await _home.Put(
            new StoredMessage(Harbor, root, root, "U0ADA", $"<@U0JOI> where is the {word}?", Mine: false, NamesMe: true),
            new StoredMessage(Harbor, MessagesHome.Ts(at.AddHours(1)), root, "U0JOI", "pier 3", Mine: true, NamesMe: false));
        Assert.Equal(0L, _home.Scalar($"SELECT needs_reply FROM thread_state WHERE thread_ts = '{root.Value}'"));
        return Assert.Single(keys);
    }

    private async Task Append(string type, string payload)
    {
        _grant ??= await _harness.HoldLease();
        var scope = EventScope.ControlPlane;
        var envelope = EventContext.Create(scope.Actor, scope.Tenant, scope.Ring, _harness.Ids.NewId()).Value.Envelope(type, payload, _harness.Clock, _harness.Ids);
        var appended = await _harness.Store.Append(new LedgerEntry(envelope, string.Create(CultureInfo.InvariantCulture, $"test:{type}:{++_events}"), Item: null), _grant, Cancel);
        Assert.True(appended.IsSuccess);
    }

    private async Task<Result<ScheduledResult>> Run()
    {
        _grant ??= await _harness.HoldLease();
        return await new RetainMessagesService(_home.Store, new LedgerSnoozes(_harness.Store), _harness.Clock).Run(new ScheduledRun(_grant, Now), Cancel);
    }
}
