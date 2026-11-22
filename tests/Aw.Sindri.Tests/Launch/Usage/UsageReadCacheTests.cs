using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Infrastructure.Launch.Usage;
using Aw.Sindri.Testing;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Tests.Launch.Usage;

/// <summary>#3420: the Claude Code reader's read state is kept on disk between runs, written only when it changed, and never stops a read.</summary>
[Lane(TestLane.Integration)]
public sealed class UsageReadCacheTests : IDisposable
{
    private static readonly DateTimeOffset _from = new(2026, 10, 17, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _to = _from.AddDays(1);
    private readonly UsageFixture _files = new();
    private readonly UsageFixture _saved = new();

    public void Dispose()
    {
        _files.Dispose();
        _saved.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Line(string id, long input) => UsageFixture.Claude(id, "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", input, 0, 0, 0);

    private ClaudeCodeUsage Reader(UsageReadCache cache) => new(_files.Root, [], cache);

    [Fact]
    public async Task A_pass_with_nothing_appended_writes_nothing()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        var cache = new UsageReadCache(_saved.Root);
        var reader = Reader(cache);

        _ = await reader.Between(_from, _to, Token);
        var afterFirst = cache.Writes;
        _ = await reader.Between(_from, _to, Token);

        Expect.Equal((1, 1), (afterFirst, cache.Writes));
    }

    [Fact]
    public async Task An_append_after_a_restart_is_counted_once()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        _ = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        _files.Write("-repo/a.jsonl", Line("m1", 10), Line("m2", 5));
        var restarted = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal(15L, Assert.Single(restarted).Input);
    }

    [Fact]
    public async Task A_corrupt_cache_file_is_dropped_and_the_reader_reads_from_scratch()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        await File.WriteAllTextAsync(Path.Combine(_saved.Root, "claude-code.db"), "this is not a database, only text", Token);

        var used = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);
        var again = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal((10L, 10L), (Assert.Single(used).Input, Assert.Single(again).Input));
    }

    [Fact]
    public async Task A_transcript_that_is_gone_leaves_the_cache_at_the_next_save()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        _files.Write("-repo/b.jsonl", Line("m2", 5));
        var reader = Reader(new UsageReadCache(_saved.Root));
        _ = await reader.Between(_from, _to, Token);
        Expect.Equal(2, new UsageReadCache(_saved.Root).Load("claude-code").Count);

        File.Delete(Path.Combine(_files.Root, "-repo", "b.jsonl"));
        _ = await reader.Between(_from, _to, Token);

        var left = new UsageReadCache(_saved.Root).Load("claude-code");
        Assert.EndsWith("a.jsonl", Assert.Single(left).Key, StringComparison.Ordinal);
        Expect.Equal(1, Assert.Single(left).Value.Messages.Count);
    }

    /// <summary>#3420: the read state outlives the process. A reader built after a restart resumes at the byte the last one stopped at, so it reads only what was appended since.</summary>
    [Fact]
    public async Task A_restarted_reader_reads_only_what_was_appended_since_the_last_run()
    {
        var saved = _saved;
        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 0));
        var first = await Reader(new UsageReadCache(saved.Root)).Between(_from, _to, Token);
        Expect.Equal(10L, Assert.Single(first).Input);

        // same byte length, so only a reader that re-reads from byte 0 sees the 99
        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 99, 0, 0, 0));
        var restarted = await Reader(new UsageReadCache(saved.Root)).Between(_from, _to, Token);

        Expect.Equal(10L, Assert.Single(restarted).Input);
    }

    /// <summary>#3420: the cache holds each message with its timestamp, so a restarted reader applies the window to what it loaded and reads no bytes again.</summary>
    [Fact]
    public async Task After_local_midnight_a_restarted_reader_counts_the_new_day_without_reading_again()
    {
        var saved = _saved;
        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 0));
        _ = await Reader(new UsageReadCache(saved.Root)).Between(_from, _to, Token);
        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 99, 0, 0, 0));   // a re-read would see 99
        var restarted = Reader(new UsageReadCache(saved.Root));

        var nextDay = await restarted.Between(_to, _to.AddDays(1), Token);
        var firstDay = await restarted.Between(_from, _to, Token);

        Assert.Empty(nextDay);
        Expect.Equal(10L, Assert.Single(firstDay).Input);
    }

    /// <summary>#3420: a cache folder that can't be used starts the reader empty; it never fails the spend read.</summary>
    [Fact]
    public async Task A_cache_folder_that_is_a_file_does_not_stop_the_read()
    {
        var saved = _saved;
        var notAFolder = Path.Combine(saved.Root, "usage-cache");
        await File.WriteAllTextAsync(notAFolder, "a file where the cache folder should be", Token);
        _files.Write("-repo/a.jsonl", UsageFixture.Claude("m1", "2026-10-17T08:00:00.000Z", "claude-sonnet-4-5", 10, 0, 0, 0));

        var used = await Reader(new UsageReadCache(notAFolder)).Between(_from, _to, Token);

        Expect.Equal(10L, Assert.Single(used).Input);
    }

    /// <summary>A transcript cut short while serve was down is read again from byte 0 after the restart, not from the saved offset.</summary>
    [Fact]
    public async Task A_transcript_truncated_while_serve_was_down_is_read_again_from_the_start()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10), Line("m2", 20));
        _ = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        _files.Write("-repo/a.jsonl", Line("m9", 7));
        var restarted = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal(7L, Assert.Single(restarted).Input);
    }

    /// <summary>A cache file stamped with another user_version is deleted at Load and the reader starts empty.</summary>
    [Fact]
    public async Task A_cache_with_another_user_version_is_dropped_and_the_reader_starts_empty()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        _ = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);
        var db = Path.Combine(_saved.Root, "claude-code.db");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99";
            _ = command.ExecuteNonQuery();
        }

        Assert.Empty(new UsageReadCache(_saved.Root).Load("claude-code"));
        Assert.False(File.Exists(db));

        // same byte length: only a reader that starts empty and reads from byte 0 sees the 99
        _files.Write("-repo/a.jsonl", Line("m1", 99));
        var restarted = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal(99L, Assert.Single(restarted).Input);
    }

    /// <summary>A transcript that is gone by the time of the restart leaves the answer and the cache at the first pass.</summary>
    [Fact]
    public async Task Rows_whose_transcript_is_gone_at_load_are_dropped()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        _files.Write("-repo/b.jsonl", Line("m2", 5));
        _ = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);
        File.Delete(Path.Combine(_files.Root, "-repo", "b.jsonl"));

        var restarted = await Reader(new UsageReadCache(_saved.Root)).Between(_from, _to, Token);

        Expect.Equal(10L, Assert.Single(restarted).Input);
        var left = new UsageReadCache(_saved.Root).Load("claude-code");
        Assert.EndsWith("a.jsonl", Assert.Single(left).Key, StringComparison.Ordinal);
    }

    private static long Count(string db, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>A second save after one new line inserts one message row, not the whole transcript again.</summary>
    [Fact]
    public void A_second_save_after_one_new_line_inserts_one_row()
    {
        var cache = new UsageReadCache(_saved.Root);
        var db = Path.Combine(_saved.Root, "claude-code.db");
        var state = new FileState { Offset = 10, Dirty = true };
        state.Messages["m1"] = (null, new ModelTokens("claude-sonnet-4-5", 10, 0, 0, 0));
        state.Messages["m2"] = (null, new ModelTokens("claude-sonnet-4-5", 20, 0, 0, 0));
        var files = new Dictionary<string, FileState>(StringComparer.Ordinal) { ["a.jsonl"] = state };
        cache.Save("claude-code", files);
        state.MarkSaved();

        // a log of every insert from now on, kept by the cache file itself
        _ = Count(db, "CREATE TABLE inserted (id TEXT); CREATE TRIGGER logged AFTER INSERT ON messages BEGIN INSERT INTO inserted VALUES (NEW.id); END; SELECT 0");
        state.Messages["m3"] = (null, new ModelTokens("claude-sonnet-4-5", 30, 0, 0, 0));
        _ = state.ChangedIds.Add("m3");
        state.Offset = 20;
        state.Dirty = true;
        cache.Save("claude-code", files);

        Expect.Equal((1L, 3L), (Count(db, "SELECT COUNT(*) FROM inserted"), Count(db, "SELECT COUNT(*) FROM messages")));
        Expect.Equal(20L, Count(db, "SELECT offset FROM files"));
        _ = Count(db, "DROP TRIGGER logged; DROP TABLE inserted; SELECT 0");
    }

    /// <summary>A cache file of another version is dropped at the first failed save and written fresh on the next pass; it does not fail every pass after.</summary>
    [Fact]
    public async Task A_save_into_a_cache_of_another_version_drops_it_and_the_next_pass_writes_a_fresh_one()
    {
        _files.Write("-repo/a.jsonl", Line("m1", 10));
        var reader = Reader(new UsageReadCache(_saved.Root));
        _ = await reader.Between(_from, _to, Token);
        var db = Path.Combine(_saved.Root, "claude-code.db");
        _ = Count(db, "PRAGMA user_version = 99; SELECT 0");

        await File.AppendAllLinesAsync(Path.Combine(_files.Root, "-repo", "a.jsonl"), [Line("m2", 5)], Token);
        _ = await reader.Between(_from, _to, Token);   // the save fails here: the file is dropped
        Assert.False(File.Exists(db));
        _ = await reader.Between(_from, _to, Token);   // and is written again, whole

        Expect.Equal(2L, Count(db, "SELECT COUNT(*) FROM messages"));
        Expect.Equal(1L, Count(db, "PRAGMA user_version"));
    }
}
