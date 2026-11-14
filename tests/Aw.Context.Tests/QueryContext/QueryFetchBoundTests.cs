using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Aw.Kernel;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// #3085: a change request matched thousands of doc chunks, every document was fetched in turn (two git reads each, ~45 ms),
/// and the 10 s deadline always ran out, so repo-docs came back SND-CONTEXT-010 with most of the answer missing. There is no
/// page limit and no fetch cap (owner q89/q93): the fix reads each document once and reads them together.
/// </summary>
public sealed class QueryFetchBoundTests
{
    private const int Matching = 500;
    private const int Docs = 50;

    [Fact, Trait("Lane", "unit")]
    public async Task Chunks_of_one_document_are_read_once_and_in_one_batch()
    {
        var docs = SourceOf([.. Enumerable.Range(1, Matching).Select(i => Chunk(i % Docs, i))]);   // 500 chunks over 50 documents

        _ = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(), TestContext.Current.CancellationToken);

        Assert.InRange(docs.DocReads, 1, Docs);   // why: a document read per chunk was 10 reads of each document
        Assert.Equal(1, docs.ReadCalls);   // why: one call to the source for the query, not one process per document
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Every_matching_hit_is_asked_for_and_a_window_of_all_reads_them_all()
    {
        var docs = SourceOf([.. Enumerable.Range(1, Matching).Select(i => Chunk(i, i))]);   // 500 documents, one chunk each

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation") { Window = new QueryWindow(0, Matching) }, Engines.Human(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(SourceQuery.DefaultLimit, Assert.Single(docs.Searches).Limit);   // the search itself has no page limit
        Assert.Equal(Matching, docs.BatchedReads);   // the window of all 500 reads every match, through the batched call
        Assert.Equal(Matching, result.Value.Sections.Sum(static s => s.Items.Count));   // and all 500 answered (a section is a named group; the records are its items)
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Many_matching_docs_at_45_ms_a_batch_still_answer_inside_the_deadline()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
        var docs = SourceOf([.. Enumerable.Range(1, Matching).Select(i => Chunk(i, i))]);
        docs.OnFetch = () => clock.Advance(TimeSpan.FromMilliseconds(45));   // the median of git cat-file -e + blob on the ux home, per document
        docs.OnFetchMany = () => clock.Advance(TimeSpan.FromMilliseconds(45));   // one git cat-file --batch per query
        var engine = Engines.Over(new FixedSourceCatalog(docs), time: clock);

        var result = await engine.Query(new ContextQuery("orientation"), Engines.Human(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Degraded, d => d.Source == RepoDocs.Id);
        Assert.NotEmpty(result.Value.Sections);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_source_on_the_default_batch_keeps_what_it_read_when_the_deadline_fires_and_the_rest_is_noted_unread()
    {
        const int total = 30;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
        var inner = SourceOf([.. Enumerable.Range(1, total).Select(i => Chunk(i, i))]);
        inner.OnFetch = () => clock.Advance(TimeSpan.FromSeconds(1));   // 10 s deadline: it fires after about ten reads
        var engine = Engines.Over(new FixedSourceCatalog(new DefaultBatchSource(inner)), time: clock);

        var result = await engine.Query(new ContextQuery("orientation") { Window = new QueryWindow(0, total) }, Engines.Human(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var read = inner.DocReads;
        Assert.InRange(read, 1, total - 1);
        Assert.Equal(read, result.Value.Sections.Sum(static s => s.Items.Count));   // every record read is in the answer
        var note = Assert.Single(result.Value.Degraded, d => d.Source == RepoDocs.Id);
        Assert.Contains((total - read).ToString(CultureInfo.InvariantCulture) + " matching record(s)", note.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_batch_the_deadline_cuts_off_is_noted_unread_not_reported_as_a_broken_source()
    {
        const int total = 40;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
        var docs = SourceOf([.. Enumerable.Range(1, total).Select(i => Chunk(i, i))]);
        docs.OnFetchMany = () => clock.Advance(TimeSpan.FromSeconds(11));   // the 10 s deadline fires inside the one batched read
        docs.FetchHangs = true;
        var engine = Engines.Over(new FixedSourceCatalog(docs), time: clock);

        var result = await engine.Query(new ContextQuery("orientation") { Window = new QueryWindow(0, total) }, Engines.Human(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var note = Assert.Single(result.Value.Degraded, d => d.Source == RepoDocs.Id);
        Assert.Contains(total.ToString(CultureInfo.InvariantCulture) + " matching record(s)", note.Message, StringComparison.Ordinal);   // why: on main, the hits the deadline cut off were counted as unread, not a source failure
    }

    // A source that does not override FetchMany, so the interface's default (one Fetch after another) runs.
    private sealed class DefaultBatchSource(FakeContextSource inner) : IContextSource
    {
        public SourceDescriptor Describe() => inner.Describe();

        public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken) => inner.Search(query, cancellationToken);

        public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken) => inner.Fetch(reference, cancellationToken);

        public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken) => inner.Versions(refs, cancellationToken);

        public ValueTask<SourceHealth> Probe(CancellationToken cancellationToken) => inner.Probe(cancellationToken);
    }

    private static FakeRecord Chunk(int doc, int chunk)
    {
        var path = "docs/notes/" + doc.ToString("D3", CultureInfo.InvariantCulture) + ".md";
        return FakeContextSource.Chunk(path, chunk, "The orientation toggle, chunk " + chunk.ToString(CultureInfo.InvariantCulture) + ".", "v" + doc.ToString("D3", CultureInfo.InvariantCulture));
    }

    private static FakeContextSource SourceOf(IReadOnlyList<FakeRecord> records) =>
        new(new SourceDescriptor(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false), records);
}
