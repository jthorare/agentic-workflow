using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// #3370: a query reads only the page of ranked records its caller asked for. Before #3243 the 10 s deadline cut a 268-match
/// answer to its first ~13 reads (SND-CONTEXT-010); after it every match was read, so an MCP page of 25 still read all 268.
/// With a window, the query reads ranks offset + 1 to offset + limit and says how many matched. Without one it still reads
/// every match (#938, owner q89/q93: no ceiling).
/// </summary>
public sealed class QueryFetchWindowTests
{
    private const int Matching = 268;   // the aw-05 shape of the scoping bench

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_window_of_25_over_268_matches_reads_25_and_counts_all_268()
    {
        var docs = SourceOf(Matching);

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation") { Window = new QueryWindow(0, 25) }, Engines.Human(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(25, docs.BatchedReads);   // why: an MCP page of 25 read all 268 records after #3243
        Assert.Equal(25, Records(result.Value).Count);
        Assert.Equal(new RecordMatches(Matched: Matching, Offset: 0, Read: 25), result.Value.Matches);
        Assert.DoesNotContain(result.Value.Degraded, d => d.Source == RepoDocs.Id);   // the rest is unread by choice, not a missed deadline
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_second_page_reads_only_ranks_101_to_200()
    {
        var whole = await Engines.Over(SourceOf(Matching)).Query(new ContextQuery("orientation"), Engines.Human(), Token);
        var docs = SourceOf(Matching);

        var page = await Engines.Over(docs).Query(new ContextQuery("orientation") { Window = new QueryWindow(100, 100) }, Engines.Human(), Token);

        Assert.True(page.IsSuccess);
        Assert.Equal(100, docs.BatchedReads);   // why: not ranks 1 to 200
        Assert.Equal(Records(whole.Value).Skip(100).Take(100).Select(static i => i.Text), Records(page.Value).Select(static i => i.Text), StringComparer.Ordinal);
        Assert.Equal(new RecordMatches(Matched: Matching, Offset: 100, Read: 100), page.Value.Matches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_window_past_the_last_match_reads_nothing_and_still_counts_the_matches()
    {
        var docs = SourceOf(Matching);

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation") { Window = new QueryWindow(300, 100) }, Engines.Human(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, docs.BatchedReads);
        Assert.Empty(Records(result.Value));
        Assert.Equal(new RecordMatches(Matched: Matching, Offset: 300, Read: 0), result.Value.Matches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_window_reads_every_match_and_leaves_the_count_out()
    {
        var docs = SourceOf(Matching);

        var result = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(Matching, docs.BatchedReads);   // #938: no limit = the whole answer
        Assert.Null(result.Value.Matches);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_item_no_sync_linked_reads_every_match_so_a_record_ranked_2nd_still_answers_through_a_window_of_1()
    {
        // why: the record that carries the tracker key may rank past the page; a windowed fetch would read rank 1 only and wrongly say unknown
        var docs = new FakeContextSource(new SourceDescriptor(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false),
        [
            FakeContextSource.Chunk("docs/notes/001.md", 1, "zeta 77 is mentioned here with the key words.", "v001"),
            FakeContextSource.Chunk("docs/notes/002.md", 2, "zeta carries the key.", "v002") with { Aliases = ["ZETA-77"] },
        ]);

        var result = await Engines.Over(docs).Query(new ContextQuery(Text: null, Item: "ZETA-77") { Window = new QueryWindow(0, 1) }, Engines.Human(), Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, docs.BatchedReads);   // the window is ignored for an unlinked key
        Assert.Contains(Records(result.Value), static i => i.Text != null && i.Text.Contains("zeta carries the key", StringComparison.Ordinal));
        Assert.Null(result.Value.Matches);
    }

    private static List<ResultItem> Records(ContextResult result) =>
        [.. result.Sections.Where(static s => s.Name == "records").SelectMany(static s => s.Items)];

    private static FakeContextSource SourceOf(int count) =>
        new(new SourceDescriptor(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false),
            [.. Enumerable.Range(1, count).Select(static i => FakeContextSource.Chunk("docs/notes/" + i.ToString("D3", CultureInfo.InvariantCulture) + ".md", i,
                "The orientation toggle, note " + i.ToString(CultureInfo.InvariantCulture) + ".", "v" + i.ToString("D3", CultureInfo.InvariantCulture)))]);
}
