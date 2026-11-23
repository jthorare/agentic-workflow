using Aw.Kernel.Data;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Infrastructure.Messages;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1c (messages spec §8.1, W7): messages.db recomputes Needs reply per thread, never the whole list; its migrations, FTS5 and purge.</summary>
[Lane(TestLane.Integration)]
public sealed class MessagesStoreTests : IDisposable
{
    private const string Harbor = "C0HARBOR";
    private const string AdaRoot = "1767771000.000100";
    private const string LeeRoot = "1767772000.000100";

    private readonly MessagesHome _home = new(new FakeTimeProvider(KnownInstants.Epoch));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose() => _home.Dispose();

    [Fact]
    public async Task Needs_reply_rebuild_touches_only_the_changed_thread()
    {
        // two threads of one channel: Ada's names me, Lee's does not
        await _home.Conversation("T0HARBOR", Harbor, ConversationKind.Channel);
        var seeded = await _home.Put(
            new StoredMessage(Harbor, new MessageTs(AdaRoot), new MessageTs(AdaRoot), "U0ADA", "<@U0JOI> review #3120 when you can", Mine: false, NamesMe: true),
            new StoredMessage(Harbor, new MessageTs(LeeRoot), ThreadTs: null, "U0LEE", "lunch Friday", Mine: false, NamesMe: false));
        Assert.Equal(["slack:thread:T0HARBOR/C0HARBOR/1767771000.000100", "slack:thread:T0HARBOR/C0HARBOR/1767772000.000100"], seeded, StringComparer.Ordinal);
        var leeRev = Rev(LeeRoot);

        // Kim replies in Ada's thread: the write rebuilds that thread only, and so does a rebuild asked for by hand
        var replied = await _home.Put(new StoredMessage(Harbor, new MessageTs("1767771500.000100"), new MessageTs(AdaRoot), "U0KIM", "can you check the group path?", Mine: false, NamesMe: true));
        var touched = await _home.Store.RebuildNeedsReply(Harbor, new MessageTs(AdaRoot), Cancel);

        Assert.Equal(["slack:thread:T0HARBOR/C0HARBOR/1767771000.000100"], replied, StringComparer.Ordinal);
        Assert.True(touched.IsSuccess);
        Assert.Equal(["slack:thread:T0HARBOR/C0HARBOR/1767771000.000100"], touched.Value, StringComparer.Ordinal);
        Assert.Equal(leeRev, Rev(LeeRoot));
        Assert.Equal(3L, Rev(AdaRoot));
        Assert.Equal(1L, _home.Scalar($"SELECT needs_reply FROM thread_state WHERE conversation = '{Harbor}' AND thread_ts = '{AdaRoot}'"));
        Assert.Equal("1767771500.000100", _home.Scalar($"SELECT newest_ts FROM thread_state WHERE conversation = '{Harbor}' AND thread_ts = '{AdaRoot}'"));
        Assert.Equal(0L, _home.Scalar($"SELECT needs_reply FROM thread_state WHERE conversation = '{Harbor}' AND thread_ts = '{LeeRoot}'"));
    }

    [Fact]
    public async Task My_reply_takes_the_thread_out_of_needs_reply_and_a_dm_is_one_row()
    {
        await _home.Conversation("T0HARBOR", "D0SAM", ConversationKind.Dm);
        var asked = await _home.Put(new StoredMessage("D0SAM", new MessageTs("1767771000.000100"), ThreadTs: null, "U0SAM", "can you ship the fare table today?", Mine: false, NamesMe: false));
        Assert.Equal(["slack:dm:T0HARBOR/D0SAM"], asked, StringComparer.Ordinal);
        Assert.Equal(1L, _home.Scalar("SELECT needs_reply FROM thread_state WHERE conversation = 'D0SAM' AND thread_ts = ''"));

        var answered = await _home.Put(new StoredMessage("D0SAM", new MessageTs("1767771100.000100"), ThreadTs: null, "U0JOI", "on it", Mine: true, NamesMe: false));

        Assert.Equal(["slack:dm:T0HARBOR/D0SAM"], answered, StringComparer.Ordinal);
        Assert.Equal(0L, _home.Scalar("SELECT needs_reply FROM thread_state WHERE conversation = 'D0SAM' AND thread_ts = ''"));
        Assert.Equal("1767771100.000100", _home.Scalar("SELECT my_last_ts FROM thread_state WHERE conversation = 'D0SAM' AND thread_ts = ''"));
    }

    [Fact]
    public async Task A_message_for_an_unknown_conversation_is_refused()
    {
        var put = await _home.Store.PutMessages([new StoredMessage("C0NOWHERE", new MessageTs(AdaRoot), ThreadTs: null, "U0ADA", "hi", Mine: false, NamesMe: false)], Cancel);

        Assert.True(put.IsError);
        Assert.Equal("SND-MSG-011", put.Error.Code);
    }

    [Fact]
    public async Task Messages_db_migrates_in_wal_with_fts5_and_a_second_run_applies_nothing()
    {
        var runner = new MigrationRunner(new SqliteConnectionFactory(new SqliteOptions(_home.Database.Path)), new FakeTimeProvider(), backupBeforeUpgrade: false);

        var first = await runner.Migrate(MessagesDatabase.Migrations, Cancel);
        var second = await runner.Migrate(MessagesDatabase.Migrations, Cancel);

        Assert.True(first.IsSuccess);
        Assert.Equal([1], first.Value.Applied);
        Assert.True(second.IsSuccess);
        Assert.Empty(second.Value.Applied);
        Assert.Equal("wal", _home.Scalar("PRAGMA journal_mode"));
        Assert.Equal(1L, _home.Scalar("SELECT COUNT(*) FROM sqlite_master WHERE name = 'messages_fts'"));
        Assert.True((await _home.Database.Writable(Cancel)).IsSuccess);   // the store's own open of a migrated file applies nothing and works
        Assert.Equal(1L, _home.Scalar("SELECT COUNT(*) FROM schema_version"));
        Assert.False(File.Exists(Path.Combine(_home.Home, "views.db")));   // its own file: nothing of it lands in views.db
    }

    [Fact]
    public async Task Purge_clears_messages_fts_rows_and_derived_columns_and_keeps_owner_flags()
    {
        await _home.Conversation("T0HARBOR", Harbor, ConversationKind.Channel);
        _ = await _home.Put(
            new StoredMessage(Harbor, new MessageTs(AdaRoot), new MessageTs(AdaRoot), "U0ADA", "harbor fares", Mine: false, NamesMe: true),
            new StoredMessage(Harbor, new MessageTs("1767771100.000100"), new MessageTs(AdaRoot), "U0JOI", "looking", Mine: true, NamesMe: false));
        Assert.True((await _home.Store.PutThreadState(new ThreadOwnerState(Harbor, new MessageTs(AdaRoot), Later: true, Follow: false), Cancel)).IsSuccess);
        _home.Exec($"UPDATE thread_state SET gist = 'Ada asks for a review', gist_key = 'k1' WHERE conversation = '{Harbor}'");
        Assert.Equal(1L, _home.Matches("fares"));

        var purged = await _home.Store.Purge(conversation: null, ownerState: false, Cancel);

        Assert.True(purged.IsSuccess);
        Assert.Equal(2, purged.Value);
        Assert.Equal(0L, _home.Count("SELECT COUNT(*) FROM messages"));
        Assert.Equal(0L, _home.Matches("fares"));
        Assert.Equal(1L, _home.Count($"SELECT COUNT(*) FROM thread_state WHERE gist IS NULL AND gist_key IS NULL AND my_last_ts IS NULL AND later = 1 AND thread_ts = '{AdaRoot}'"));
    }

    [Fact]
    public async Task A_slack_delete_takes_the_message_and_its_fts_row()
    {
        await _home.Conversation("T0HARBOR", Harbor, ConversationKind.Channel);
        _ = await _home.Put(new StoredMessage(Harbor, new MessageTs(LeeRoot), ThreadTs: null, "U0LEE", "ferry timetable", Mine: false, NamesMe: false));

        var removed = await _home.Store.RemoveMessage(Harbor, new MessageTs(LeeRoot), Cancel);

        Assert.True(removed.IsSuccess);
        Assert.Equal(["slack:thread:T0HARBOR/C0HARBOR/1767772000.000100"], removed.Value, StringComparer.Ordinal);
        Assert.Equal(0L, _home.Matches("timetable"));
        Assert.Null(_home.Scalar($"SELECT newest_ts FROM thread_state WHERE thread_ts = '{LeeRoot}'"));
    }

    [Fact]
    public async Task An_edited_message_is_found_by_its_new_text_and_not_its_old_text()
    {
        await _home.Conversation("T0HARBOR", Harbor, ConversationKind.Channel);
        _ = await _home.Put(new StoredMessage(Harbor, new MessageTs(LeeRoot), ThreadTs: null, "U0LEE", "ferry timetable", Mine: false, NamesMe: false));

        _ = await _home.Put(new StoredMessage(Harbor, new MessageTs(LeeRoot), ThreadTs: null, "U0LEE", "ferry schedule", Mine: false, NamesMe: false));

        Assert.Equal(1L, _home.Count("SELECT COUNT(*) FROM messages"));
        Assert.Equal(1L, _home.Matches("schedule"));
        Assert.Equal(0L, _home.Matches("timetable"));
    }

    private long Rev(string root) => _home.Count($"SELECT rev FROM thread_state WHERE conversation = '{Harbor}' AND thread_ts = '{root}'");
}
