using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>What only the SQL store can show: full-text rows, vector orphans, links, and the file's own bytes.</summary>
public sealed class SqliteRecordStoreTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task A_committed_chunk_is_found_by_its_words_until_its_record_is_tombstoned()
    {
        var record = RecordSamples.Record("linear:issue/1");
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "split pane orientation")), TestContext.Current.CancellationToken);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM chunk_fts WHERE chunk_fts MATCH '\"orientation\"'"));

        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [record.Key] }, TestContext.Current.CancellationToken);
        Assert.Equal(0, _world.Count("SELECT count(*) FROM chunk_fts WHERE chunk_fts MATCH '\"orientation\"'"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task No_vector_outlives_its_chunk_after_a_rechunk_a_tombstone_or_a_purge()
    {
        var a = RecordSamples.Record("linear:issue/a", "v1");
        var b = RecordSamples.Record("linear:issue/b");
        var c = RecordSamples.Record("linear:issue/c");
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(a, "a0", "a1"), RecordSamples.Write(b, "b0"), RecordSamples.Write(c, "c0")), TestContext.Current.CancellationToken);
        _ = await _world.Store.PutVectors(
            [RecordSamples.Vector(new ChunkKey(a.Key, 0), "a0", 1, 0), RecordSamples.Vector(new ChunkKey(a.Key, 1), "a1", 0, 1), RecordSamples.Vector(new ChunkKey(b.Key, 0), "b0", 1, 1), RecordSamples.Vector(new ChunkKey(c.Key, 0), "c0", 1, 2)],
            TestContext.Current.CancellationToken);
        const string orphans = "SELECT count(*) FROM embedding WHERE space = 'chunk' AND key NOT IN (SELECT key FROM chunk)";

        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2, RecordSamples.Write(RecordSamples.Record("linear:issue/a", "v2"), "a0 changed")), TestContext.Current.CancellationToken);
        Assert.Equal(0, _world.Count(orphans));
        Assert.Equal(2, _world.Count("SELECT count(*) FROM embedding"));

        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 3) with { Removed = [b.Key] }, TestContext.Current.CancellationToken);
        Assert.Equal(0, _world.Count(orphans));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM embedding"));

        _ = await _world.Store.Purge(RecordSamples.Linear, TestContext.Current.CancellationToken);
        Assert.Equal(0, _world.Count("SELECT count(*) FROM embedding"));
        Assert.Equal(0, _world.Count("SELECT count(*) FROM chunk"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Links_resolve_both_ways_and_a_tombstone_clears_the_titles_that_named_it()
    {
        var parent = RecordSamples.Record("linear:issue/parent");
        var child = RecordSamples.Record("linear:issue/child") with { Links = [new RecordLink("linear:issue/parent", RecordLinkKind.Parent, "Parent title")] };
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(child, "child words")), TestContext.Current.CancellationToken);
        Assert.Null((await _world.Store.Get(child.Key, TestContext.Current.CancellationToken)).Value!.Links.Single().ToKey);

        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2, RecordSamples.Write(parent, "parent words")), TestContext.Current.CancellationToken);
        var resolved = (await _world.Store.Get(child.Key, TestContext.Current.CancellationToken)).Value!.Links.Single();
        Assert.Equal(parent.Key, resolved.ToKey);
        Assert.Equal("Parent title", resolved.Title);

        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 3) with { Removed = [parent.Key] }, TestContext.Current.CancellationToken);
        var after = (await _world.Store.Get(child.Key, TestContext.Current.CancellationToken)).Value!.Links.Single();
        Assert.Equal(parent.Key, after.ToKey);
        Assert.Null(after.Title);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tombstone_keeps_only_the_key_state_and_times()
    {
        var record = RecordSamples.Record("linear:issue/1") with { Url = "https://linear.app/x/issue/1", Author = "Ada" };
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words")), TestContext.Current.CancellationToken);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [record.Key] }, TestContext.Current.CancellationToken);

        var row = (await _world.Store.Get(record.Key, TestContext.Current.CancellationToken)).Value!;
        Assert.Equal(record.Key.Value, row.Title);
        Assert.Null(row.Url);
        Assert.Null(row.Author);
        Assert.Null(row.StateRaw);
        Assert.Equal(RecordState.Open, row.State);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_tombstoned_record_that_comes_back_is_written_again()
    {
        var record = RecordSamples.Record("linear:issue/1");
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words")), TestContext.Current.CancellationToken);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [record.Key] }, TestContext.Current.CancellationToken);

        var back = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 3, RecordSamples.Write(record, "words")), TestContext.Current.CancellationToken);
        Assert.Equal(new CommitReport(1, 0, 0), back.Value);
        Assert.Null((await _world.Store.Get(record.Key, TestContext.Current.CancellationToken)).Value!.RemovedAt);
        _ = Assert.Single((await _world.Store.Chunks(record.Key, TestContext.Current.CancellationToken)).Value);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_purge_removes_the_intent_rows_of_its_source_and_no_other()
    {
        var mine = RecordSamples.Record("linear:issue/1");
        var theirs = RecordSamples.Record("github:pr/o/r/1", source: RecordSamples.Github);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(mine, "words")), TestContext.Current.CancellationToken);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(theirs, "other")), TestContext.Current.CancellationToken);
        Assert.Equal(2, _world.Count("SELECT count(*) FROM intent_record"));

        _ = await _world.Store.Purge(RecordSamples.Linear, TestContext.Current.CancellationToken);
        Assert.Equal(1, _world.Count("SELECT count(*) FROM intent_record"));
        Assert.Equal(1, _world.Count("SELECT count(*) FROM intent_record WHERE source = 'github'"));
    }

    [Fact, Trait("Lane", "heavy")]
    public async Task A_removed_text_is_not_left_in_the_database_file_or_its_wal()
    {
        const string needle = "zebra-needle-q7x4";
        var record = RecordSamples.Record("linear:issue/1");
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, $"the {needle} was planted here")), TestContext.Current.CancellationToken);
        _ = await _world.Store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [record.Key] }, TestContext.Current.CancellationToken);

        foreach (var file in new[] { _world.DatabasePath, _world.DatabasePath + "-wal" }.Where(File.Exists))
        {
            await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(needle, System.Text.Encoding.Latin1.GetString(memory.ToArray()), StringComparison.Ordinal);
        }
    }
}
