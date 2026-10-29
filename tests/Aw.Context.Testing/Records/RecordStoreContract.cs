using Aw.Context.Core;
using Aw.Context.Core.Audience;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Xunit;

namespace Aw.Context.Testing.Records;

/// <summary>
/// The contract every <see cref="IRecordStore"/> honours (spec §4, §5.5): a page commits all or nothing, a replay changes
/// nothing, a changed text replaces its chunks and drops their stale vectors, a tombstone keeps the row and drops the text,
/// vectors are stored only for a chunk that still has their hash, and a purge removes one source and no other.
/// </summary>
/// <typeparam name="TStore">The implementation.</typeparam>
public abstract class RecordStoreContract<TStore>
    where TStore : IRecordStore
{
    private static readonly string _owner = typeof(TStore).Name;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract TStore Create();

    /// <summary>A committed page reads back: the record, its chunks in order, and the position.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_committed_page_reads_back_with_its_chunks_and_position()
    {
        var store = Create();
        var record = RecordSamples.Record("linear:issue/1");
        var report = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "first words", "second words")), CancellationToken.None));
        ContractCheck.That(_owner, report == new CommitReport(1, 0, 0), "the report counts one written record");

        var read = ContractCheck.Ok(_owner, await store.Get(record.Key, CancellationToken.None));
        ContractCheck.That(_owner, read == record || (read is not null && read.Key == record.Key && read.Title == record.Title && read.Version == record.Version && read.Audience == record.Audience), "the record reads back");
        var chunks = ContractCheck.Ok(_owner, await store.Chunks(record.Key, CancellationToken.None));
        ContractCheck.That(_owner, chunks.Select(c => c.Text).SequenceEqual(["first words", "second words"], StringComparer.Ordinal), "the chunks read back in Seq order");
        var position = ContractCheck.Ok(_owner, await store.Position(RecordSamples.Linear, CancellationToken.None));
        ContractCheck.That(_owner, position.Cursor == SourceCursor.From("1") && position.Phase == SyncPhase.Incremental, "the position moved with the page");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Position(RecordSamples.Github, CancellationToken.None)) == SyncPosition.Start, "another source still has its start position");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Get(RecordKey.From("linear:issue/none"), CancellationToken.None)) is null, "an unknown key reads as null");
    }

    /// <summary>A replay of a page changes nothing, not even the record stamp.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_replayed_page_changes_nothing()
    {
        var store = Create();
        var batch = RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record("linear:issue/1"), "words"));
        _ = ContractCheck.Ok(_owner, await store.Commit(batch, CancellationToken.None));
        var before = ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None));
        var replay = ContractCheck.Ok(_owner, await store.Commit(batch, CancellationToken.None));
        ContractCheck.That(_owner, replay == new CommitReport(0, 1, 0), "a replay counts the record as unchanged");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None)).DeleteEpoch == before.DeleteEpoch, "a replay removes no vector");
    }

    /// <summary>Every committed page moves the record stamp.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Each_committed_page_moves_the_record_stamp()
    {
        var store = Create();
        var first = ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(RecordSamples.Record("linear:issue/1"), "words")), CancellationToken.None));
        var second = ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None));
        ContractCheck.That(_owner, second.RecordStamp > first.RecordStamp, "the record stamp moved");
    }

    /// <summary>A changed text replaces the chunks and drops the vectors of the chunks that changed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_changed_text_replaces_its_chunks_and_drops_their_stale_vectors()
    {
        var store = Create();
        var v1 = RecordSamples.Record("linear:issue/1", "v1");
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(v1, "keep me", "old tail")), CancellationToken.None));
        var keep = new ChunkKey(v1.Key, 0);
        var stale = new ChunkKey(v1.Key, 1);
        Equal(await store.PutVectors([RecordSamples.Vector(keep, "keep me", 1, 0), RecordSamples.Vector(stale, "old tail", 0, 1)], CancellationToken.None), 2, "both vectors are stored");
        var epoch = ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None)).DeleteEpoch;

        var v2 = RecordSamples.Record("linear:issue/1", "v2");
        var report = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2, RecordSamples.Write(v2, "keep me", "new tail")), CancellationToken.None));
        ContractCheck.That(_owner, report.Written == 1, "the changed record is written");
        var rows = ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Chunk, 0, 100, CancellationToken.None));
        ContractCheck.That(_owner, rows.Select(r => r.Vector.Key).SequenceEqual([keep.ToString()], StringComparer.Ordinal), "only the vector of the unchanged chunk survives");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None)).DeleteEpoch > epoch, "removing a vector moved the delete epoch");
        var chunks = ContractCheck.Ok(_owner, await store.Chunks(v2.Key, CancellationToken.None));
        ContractCheck.That(_owner, chunks.Select(c => c.Text).SequenceEqual(["keep me", "new tail"], StringComparer.Ordinal), "the chunks are the new ones");
    }

    /// <summary>A tombstone keeps the row and drops the text and the vectors.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_tombstone_keeps_the_row_and_drops_its_chunks_and_vectors()
    {
        var store = Create();
        var record = RecordSamples.Record("linear:issue/1");
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.PutVectors([RecordSamples.Vector(new ChunkKey(record.Key, 0), "words", 1, 1)], CancellationToken.None));

        var tombstone = RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [record.Key] };
        var report = ContractCheck.Ok(_owner, await store.Commit(tombstone, CancellationToken.None));
        ContractCheck.That(_owner, report.Tombstoned == 1, "one tombstone");
        var row = ContractCheck.Ok(_owner, await store.Get(record.Key, CancellationToken.None));
        ContractCheck.That(_owner, row is { RemovedAt: not null }, "the row stays, with RemovedAt set");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Chunks(record.Key, CancellationToken.None)).Count == 0, "its chunks are gone");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Chunk, 0, 100, CancellationToken.None)).Count == 0, "its vectors are gone");
        var listed = ContractCheck.Ok(_owner, await store.List(new RecordFilter(), after: null, 10, CancellationToken.None));
        ContractCheck.That(_owner, listed.Count == 0, "a listing leaves tombstones out");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.List(new RecordFilter(IncludeRemoved: true), after: null, 10, CancellationToken.None)).Count == 1, "unless asked for them");
    }

    /// <summary>An invalid batch writes nothing: not its records, not its position.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_invalid_batch_writes_nothing()
    {
        var store = Create();
        var good = RecordSamples.Write(RecordSamples.Record("linear:issue/1"), "words");
        var foreign = RecordSamples.Write(RecordSamples.Record("github:pr/o/r/1", source: RecordSamples.Github), "words");
        ContractCheck.Fails(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, good, foreign), CancellationToken.None), ContextErrors.RecordBatchInvalid.Code);

        var blocked = new RecordWrite(RecordSamples.Record("linear:issue/2", blocked: true), [new RecordChunk(0, "text", 1, RecordSamples.Hash("text"))]);
        ContractCheck.Fails(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, good, blocked), CancellationToken.None), ContextErrors.RecordBatchInvalid.Code);

        var gap = new RecordWrite(RecordSamples.Record("linear:issue/3"), [new RecordChunk(1, "text", 1, RecordSamples.Hash("text"))]);
        ContractCheck.Fails(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, good, gap), CancellationToken.None), ContextErrors.RecordBatchInvalid.Code);

        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Get(good.Record.Key, CancellationToken.None)) is null, "the good record of a refused page was not written");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Position(RecordSamples.Linear, CancellationToken.None)) == SyncPosition.Start, "the position did not move");
    }

    /// <summary>A blocked record keeps its row and has no chunks.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_blocked_record_keeps_its_row_and_has_no_chunks()
    {
        var store = Create();
        var blocked = RecordSamples.Record("linear:issue/2", blocked: true);
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, new RecordWrite(blocked, [])), CancellationToken.None));
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Get(blocked.Key, CancellationToken.None)) is { Blocked: true }, "the row is there, blocked");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Chunks(blocked.Key, CancellationToken.None)).Count == 0, "it has no chunks");
    }

    /// <summary>A vector is stored only for a chunk that exists with the same hash; a re-embed takes a new Seq.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_vector_is_stored_only_for_its_chunk_and_a_reembed_takes_a_new_seq()
    {
        var store = Create();
        var record = RecordSamples.Record("linear:issue/1");
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(record, "words")), CancellationToken.None));
        var chunk = new ChunkKey(record.Key, 0);
        var orphan = RecordSamples.Vector(new ChunkKey(record.Key, 7), "nothing", 1, 0);
        var wrongHash = RecordSamples.Vector(chunk, "other words", 1, 0);
        Equal(await store.PutVectors([orphan, wrongHash], CancellationToken.None), 0, "a vector for no chunk, or for another hash, is not stored");

        Equal(await store.PutVectors([RecordSamples.Vector(chunk, "words", 1, 1)], CancellationToken.None), 1, "the matching vector is stored");
        var first = ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Chunk, 0, 10, CancellationToken.None)).Single();
        Equal(await store.PutVectors([RecordSamples.Vector(chunk, "words", 1, 2)], CancellationToken.None), 1, "a re-embed is stored");
        var rows = ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Chunk, 0, 10, CancellationToken.None));
        ContractCheck.That(_owner, rows.Count == 1 && rows[0].Seq > first.Seq, "one row remains, with a later Seq");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Chunk, rows[0].Seq, 10, CancellationToken.None)).Count == 0, "reading after the last Seq answers nothing");
    }

    /// <summary>The work list holds the chunks without a current vector of the model, newest record first, and skips removed records.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_work_list_is_the_chunks_without_a_current_vector_newest_record_first()
    {
        var store = Create();
        var older = RecordSamples.Record("linear:issue/1") with { UpdatedAt = RecordSamples.Now.AddDays(-2) };
        var newer = RecordSamples.Record("linear:issue/2") with { UpdatedAt = RecordSamples.Now };
        var gone = RecordSamples.Record("linear:issue/3");
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(older, "old a", "old b"), RecordSamples.Write(newer, "new a"), RecordSamples.Write(gone, "gone a")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [gone.Key] }, CancellationToken.None));

        var all = ContractCheck.Ok(_owner, await store.ChunksToEmbed("fake-model@1", 100, CancellationToken.None));
        ContractCheck.That(_owner, all.Select(c => c.Key).SequenceEqual([new ChunkKey(newer.Key, 0).ToString(), new ChunkKey(older.Key, 0).ToString(), new ChunkKey(older.Key, 1).ToString()], StringComparer.Ordinal), "newest record first, its chunks in order, the removed record left out");
        ContractCheck.That(_owner, all[0].Text == "new a" && all[0].TextHash == RecordSamples.Hash("new a"), "a row carries the text and its hash");

        Equal(await store.PutVectors([RecordSamples.Vector(new ChunkKey(newer.Key, 0), "new a", 1, 0), RecordSamples.Vector(new ChunkKey(older.Key, 0), "old a", 0, 1)], CancellationToken.None), 2, "two vectors are stored");
        var left = ContractCheck.Ok(_owner, await store.ChunksToEmbed("fake-model@1", 100, CancellationToken.None));
        ContractCheck.That(_owner, left.Select(c => c.Key).SequenceEqual([new ChunkKey(older.Key, 1).ToString()], StringComparer.Ordinal), "an embedded chunk leaves the list");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.ChunksToEmbed("other-model@1", 100, CancellationToken.None)).Count == 3, "another model starts from scratch");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.ChunksToEmbed("fake-model@1", 1, CancellationToken.None)).Count == 1, "the limit is honoured");
    }

    /// <summary>A node vector needs no chunk, and a delete by key removes it and moves the epoch.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_node_vector_is_stored_without_a_chunk_and_deleted_by_key()
    {
        var store = Create();
        var node = new VectorWrite(VectorSpace.Node, "feature:billing", "fake-model@1", RecordSamples.Hash("billing"), new float[] { 1, 0 });
        Equal(await store.PutVectors([node], CancellationToken.None), 1, "a node vector is stored");
        var epoch = ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None)).DeleteEpoch;
        Equal(await store.DeleteVectors(VectorSpace.Node, ["feature:billing", "feature:none"], CancellationToken.None), 1, "one vector is removed");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Stamps(CancellationToken.None)).DeleteEpoch > epoch, "the delete epoch moved");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Node, 0, 10, CancellationToken.None)).Count == 0, "no node vector is left");
    }

    /// <summary>A listing is in key order, pages with the last key, and filters by source.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_listing_pages_in_key_order_and_filters()
    {
        var store = Create();
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1,
            RecordSamples.Write(RecordSamples.Record("linear:issue/b"), "b"), RecordSamples.Write(RecordSamples.Record("linear:issue/a"), "a"), RecordSamples.Write(RecordSamples.Record("linear:issue/c"), "c")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(RecordSamples.Record("github:pr/o/r/1", source: RecordSamples.Github), "pr")), CancellationToken.None));

        var firstPage = ContractCheck.Ok(_owner, await store.List(new RecordFilter(RecordSamples.Linear), after: null, 2, CancellationToken.None));
        ContractCheck.That(_owner, firstPage.Select(r => r.Key.Value).SequenceEqual(["linear:issue/a", "linear:issue/b"], StringComparer.Ordinal), "the first page is the two smallest keys");
        var next = ContractCheck.Ok(_owner, await store.List(new RecordFilter(RecordSamples.Linear), firstPage[^1].Key, 2, CancellationToken.None));
        ContractCheck.That(_owner, next.Select(r => r.Key.Value).SequenceEqual(["linear:issue/c"], StringComparer.Ordinal), "the next page continues after the last key");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.List(new RecordFilter(Kind: RecordKind.Doc), after: null, 10, CancellationToken.None)).Count == 0, "a kind filter narrows");
    }

    /// <summary>A failed attempt counts, sets the retry time and keeps the last commit; a success resets them and stamps the commit; neither touches the last outcome.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_attempt_sets_and_clears_the_backoff_and_leaves_the_outcome_alone()
    {
        var store = Create();
        var at = new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Backoff(RecordSamples.Linear, CancellationToken.None)) == SourceBackoffState.None, "a source that never ticked has no backoff");

        _ = ContractCheck.Ok(_owner, await store.RecordOutcome(RecordSamples.Linear, new SyncOutcome(at, "SND-CONTEXT-010"), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.RecordAttempt(RecordSamples.Linear, new SyncAttempt(at.AddHours(-1), Failed: false, ErrorText: null), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.RecordAttempt(RecordSamples.Linear, new SyncAttempt(at, Failed: true, "down"), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.RecordAttempt(RecordSamples.Linear, new SyncAttempt(at.AddMinutes(5), Failed: true, "still down"), CancellationToken.None));

        var failing = ContractCheck.Ok(_owner, await store.Backoff(RecordSamples.Linear, CancellationToken.None));
        ContractCheck.That(_owner, failing == new SourceBackoffState(2, at.AddMinutes(15), at.AddHours(-1), "still down"), "two failures wait 10 minutes from the second and keep the last commit");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.LastOutcome(RecordSamples.Linear, CancellationToken.None)) == new SyncOutcome(at, "SND-CONTEXT-010"), "the last outcome is the caller's own");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Backoff(RecordSamples.Github, CancellationToken.None)) == SourceBackoffState.None, "another source is untouched");

        _ = ContractCheck.Ok(_owner, await store.RecordAttempt(RecordSamples.Linear, new SyncAttempt(at.AddMinutes(20), Failed: false, ErrorText: null), CancellationToken.None));
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Backoff(RecordSamples.Linear, CancellationToken.None)) == new SourceBackoffState(Failures: 0, NextTry: null, LastCommit: at.AddMinutes(20), LastErrorText: null), "a success resets the backoff and stamps the commit");
    }

    /// <summary>A count is one source's records, chunks and chunk vectors, and nothing of another source.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_count_reports_one_sources_records_chunks_and_vectors()
    {
        var store = Create();
        var first = RecordSamples.Record("linear:issue/1");
        var second = RecordSamples.Record("linear:issue/2");
        var other = RecordSamples.Record("github:pr/o/r/1", source: RecordSamples.Github);
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(first, "one", "two"), RecordSamples.Write(second, "three")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(other, "other")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.PutVectors([RecordSamples.Vector(new ChunkKey(first.Key, 0), "one", 1, 0), RecordSamples.Vector(new ChunkKey(other.Key, 0), "other", 0, 1)], CancellationToken.None));

        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Count(RecordSamples.Linear, CancellationToken.None)) == new SourceCounts(2, 3, 1), "linear holds 2 records, 3 chunks and 1 vector");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Count(RecordSamples.Github, CancellationToken.None)) == new SourceCounts(1, 1, 1), "github's rows are its own");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Count(SourceId.From("slack"), CancellationToken.None)) == new SourceCounts(0, 0, 0), "a source the store never saw holds nothing");
    }

    /// <summary>The live count leaves out tombstones and blocked records; the embedded count is the live records with a chunk vector.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_live_count_leaves_out_removed_and_blocked_records_and_counts_the_ones_with_a_vector()
    {
        var store = Create();
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.LiveCounts(CancellationToken.None)) == new LiveRecordCounts(0, 0), "an empty store holds nothing");
        var embedded = RecordSamples.Record("linear:issue/1");
        var twoChunks = RecordSamples.Record("linear:issue/2");
        var pending = RecordSamples.Record("linear:issue/3");
        var removed = RecordSamples.Record("linear:issue/4");
        var blocked = RecordSamples.Record("linear:issue/5", blocked: true);
        var github = RecordSamples.Record("github:pr/o/r/1", source: RecordSamples.Github);
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(embedded, "one"), RecordSamples.Write(twoChunks, "a", "b"), RecordSamples.Write(pending, "three"), RecordSamples.Write(removed, "four"), RecordSamples.Write(blocked)), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(github, "gh")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.PutVectors(
            [RecordSamples.Vector(new ChunkKey(embedded.Key, 0), "one", 1, 0), RecordSamples.Vector(new ChunkKey(twoChunks.Key, 0), "a", 1, 0), RecordSamples.Vector(new ChunkKey(twoChunks.Key, 1), "b", 0, 1), RecordSamples.Vector(new ChunkKey(github.Key, 0), "gh", 1, 1)],
            CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 2) with { Removed = [removed.Key] }, CancellationToken.None));

        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.LiveCounts(CancellationToken.None)) == new LiveRecordCounts(4, 3), "4 live records (one removed, one blocked), 3 with a vector: a record with two vectors counts once");
    }

    /// <summary>The project read lists a source's visible projects by title, each with its lead and the open, visible issues linked to it, and nothing the audience can't read.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_project_read_counts_open_visible_issues_and_hides_what_the_audience_cannot_read()
    {
        var store = Create();
        var projects = SourceId.From("linear-projects");
        var beta = RecordSamples.Record("linear:project/beta", source: projects) with { Kind = RecordKind.Project, Title = "Beta", Health = "atRisk", DueAt = new DateOnly(2026, 11, 14), Links = [new RecordLink("linear:user/u1", RecordLinkKind.LedBy, "Ada")] };
        var alpha = RecordSamples.Record("linear:project/alpha", source: projects) with { Kind = RecordKind.Project, Title = "Alpha" };
        var hidden = RecordSamples.Record("linear:project/hidden", source: projects) with { Kind = RecordKind.Project, Title = "Hidden", Audience = AudienceKey.From("linear:other") };
        var removed = RecordSamples.Record("linear:project/gone", source: projects) with { Kind = RecordKind.Project, Title = "Gone" };
        var inBeta = new[] { new RecordLink("linear:project/beta", RecordLinkKind.InProject, Title: null) };
        var older = RecordSamples.Now.AddDays(-1);
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(projects, 1, RecordSamples.Write(beta, "b"), RecordSamples.Write(alpha, "a"), RecordSamples.Write(hidden, "h"), RecordSamples.Write(removed, "g")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1,
            RecordSamples.Write(RecordSamples.Record("linear:issue/1") with { Links = inBeta }, "one"),
            RecordSamples.Write(RecordSamples.Record("linear:issue/2") with { Links = inBeta, UpdatedAt = older }, "two"),
            RecordSamples.Write(RecordSamples.Record("linear:issue/3") with { Links = inBeta, State = RecordState.Done }, "three"),
            RecordSamples.Write(RecordSamples.Record("linear:issue/4") with { Links = inBeta, Audience = AudienceKey.From("linear:other") }, "four")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(projects, 2) with { Removed = [removed.Key] }, CancellationToken.None));

        var rows = ContractCheck.Ok(_owner, await store.ProjectFacts(projects, new AudienceScope(new HashSet<AudienceKey> { AudienceKey.From("linear:team-a") }), CancellationToken.None));

        ContractCheck.That(_owner, rows.Select(r => r.Title).SequenceEqual(["Alpha", "Beta"], StringComparer.Ordinal), "the visible, live projects come by title (not the other audience's, not the removed one)");
        var row = rows[1];
        ContractCheck.That(_owner, row.OpenIssues == 2, "two open visible issues: not the done one, not the other audience's");
        ContractCheck.That(_owner, row.NewestIssueAt == RecordSamples.Now, "the newest change among the issues the caller can read");
        ContractCheck.That(_owner, row.LeadRef == "linear:user/u1" && row.LeadName == "Ada", "the lead is the led-by link's target and title");
        ContractCheck.That(_owner, row.Health == "atRisk" && row.DueAt == new DateOnly(2026, 11, 14), "health and due day come with it");
        ContractCheck.That(_owner, rows[0].OpenIssues == 0 && rows[0].NewestIssueAt is null && rows[0].LeadRef is null, "a project with no issues and no lead has zero, null and null");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.ProjectFacts(RecordSamples.Github, new AudienceScope(new HashSet<AudienceKey> { AudienceKey.From("linear:team-a") }), CancellationToken.None)).Count == 0, "another source has no projects here");
    }

    /// <summary>A purge removes one source's records, chunks, vectors and position and no other's.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_purge_removes_one_source_and_no_other()
    {
        var store = Create();
        var mine = RecordSamples.Record("linear:issue/1");
        var theirs = RecordSamples.Record("github:pr/o/r/1", source: RecordSamples.Github);
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Linear, 1, RecordSamples.Write(mine, "words")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.Commit(RecordSamples.Batch(RecordSamples.Github, 1, RecordSamples.Write(theirs, "other")), CancellationToken.None));
        _ = ContractCheck.Ok(_owner, await store.PutVectors([RecordSamples.Vector(new ChunkKey(mine.Key, 0), "words", 1, 0), RecordSamples.Vector(new ChunkKey(theirs.Key, 0), "other", 0, 1)], CancellationToken.None));

        Equal(await store.Purge(RecordSamples.Linear, CancellationToken.None), 1, "one record deleted");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Get(mine.Key, CancellationToken.None)) is null, "the purged record is gone, not a tombstone");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Position(RecordSamples.Linear, CancellationToken.None)) == SyncPosition.Start, "its position is gone");
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, await store.Get(theirs.Key, CancellationToken.None)) is not null, "the other source's record stays");
        var rows = ContractCheck.Ok(_owner, await store.Vectors(VectorSpace.Chunk, 0, 10, CancellationToken.None));
        ContractCheck.That(_owner, rows.Select(r => r.Vector.Key).SequenceEqual([new ChunkKey(theirs.Key, 0).ToString()], StringComparer.Ordinal), "only the other source's vector stays");
    }

    private static void Equal(Result<int> actual, int expected, string what) =>
        ContractCheck.That(_owner, ContractCheck.Ok(_owner, actual) == expected, what);
}
