using System.Globalization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Sources;
using Aw.Context.Testing.Engine;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// #3085: a change request matched thousands of doc chunks, every one was fetched in turn (two git reads each, ~45 ms), and
/// the 10 s deadline always ran out, so repo-docs came back SND-CONTEXT-010 with most of the answer missing.
/// </summary>
public sealed class QueryFetchBoundTests
{
    private const int Matching = 500;

    [Fact, Trait("Lane", "unit")]
    public async Task Every_source_is_searched_for_a_bounded_page_of_hits()
    {
        var docs = ManyMatchingDocs();

        _ = await Engines.Over(docs).Query(new ContextQuery("orientation"), Engines.Human(), TestContext.Current.CancellationToken);

        var search = Assert.Single(docs.Searches);
        Assert.InRange(search.Limit, 1, 100);   // why: int.MaxValue asked for every chunk that matches any one term
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Many_matching_docs_at_45_ms_a_fetch_still_answer_inside_the_deadline()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero));
        var docs = ManyMatchingDocs();
        docs.OnFetch = () => clock.Advance(TimeSpan.FromMilliseconds(45));   // the median of git cat-file -e + blob on the ux home
        var engine = Engines.Over(new FixedSourceCatalog(docs), time: clock);

        var result = await engine.Query(new ContextQuery("orientation"), Engines.Human(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Value.Degraded, d => d.Source == RepoDocs.Id);
        Assert.NotEmpty(result.Value.Sections);
    }

    private static FakeContextSource ManyMatchingDocs() =>
        new(new SourceDescriptor(RepoDocs.Id, SourceKind.Doc, SourceFreshness.Snapshot, SupportsAsOf: false),
            [.. Enumerable.Range(1, Matching).Select(i =>
            {
                var n = i.ToString("D3", CultureInfo.InvariantCulture);
                return FakeContextSource.Doc($"docs/notes/{n}.md", "Note " + n, "The orientation toggle, note " + n + ".", "v" + n);
            })]);
}
