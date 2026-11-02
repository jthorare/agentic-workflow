using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Host.Text;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The concepts line of 'sindri index status' (#1597): one line after the feed rows, a part dropped when its value is unknown.</summary>
public sealed class IndexTextTests
{
    private static readonly IndexSourceRow _linear = new("linear", "incremental", Usable: true, Complete: true, new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), LastCode: null);
    private const string LinearLine = "linear  complete · last sync 2026-10-27 12:00:00Z\n";

    private static string Text(IndexConceptsView? concepts) => IndexText.Status(new IndexStatusView([_linear]) { Concepts = concepts });

    [Fact, Trait("Lane", "unit")]
    public void A_server_without_concepts_prints_the_feed_rows_only() => Assert.Equal(LinearLine, Text(concepts: null));

    [Fact, Trait("Lane", "unit")]
    public void Counts_print_on_one_line_after_the_feed_rows()
    {
        var concepts = new IndexConceptsView(Enabled: true, Concepts: 101, MultiSource: 34, Queued: 3, Covered: 300, Embedded: 550, Pending: 12, Watermark: 8812, Coverage: 300d / 550);

        Assert.Equal(LinearLine + "concepts: 101 (34 cite 2+ sources), 3 queued, 300 of 550 records covered (55%), 12 not embedded yet, watermark 8812\n", Text(concepts));
    }

    [Fact, Trait("Lane", "unit")]
    public void Parts_whose_value_is_null_are_dropped()
    {
        var concepts = new IndexConceptsView(Enabled: true, Concepts: 5, MultiSource: 0, Queued: 0, Watermark: 40);

        Assert.Equal(LinearLine + "concepts: 5 (0 cite 2+ sources), 0 queued, watermark 40\n", Text(concepts));
    }

    [Fact, Trait("Lane", "unit")]
    public void Nothing_embedded_drops_the_covered_part_and_nothing_pending_drops_the_pending_part()
    {
        var concepts = new IndexConceptsView(Enabled: true, Concepts: 2, MultiSource: 1, Queued: 0, Covered: 0, Embedded: 0, Pending: 0, Watermark: 9);

        Assert.Equal(LinearLine + "concepts: 2 (1 cite 2+ sources), 0 queued, watermark 9\n", Text(concepts));
    }

    [Fact, Trait("Lane", "unit")]
    public void Off_says_which_switches_to_look_at() => Assert.Equal(LinearLine + "concepts: off (context.conceptSynthesis or labeling is off)\n", Text(new IndexConceptsView(Enabled: false, Concepts: 4)));

    [Fact, Trait("Lane", "unit")]
    public void A_failed_read_prints_its_code() => Assert.Equal(LinearLine + "concepts: unavailable (SND-CONTEXT-004)\n", Text(new IndexConceptsView(Enabled: true, Code: "SND-CONTEXT-004")));

    [Fact, Trait("Lane", "unit")]
    public void No_concept_yet_says_none_yet() => Assert.Equal(LinearLine + "concepts: none yet\n", Text(new IndexConceptsView(Enabled: true, Concepts: 0, MultiSource: 0, Queued: 0, Covered: 0, Embedded: 0, Pending: 5, Watermark: 0)));

    [Fact, Trait("Lane", "unit")]
    public void With_no_record_source_the_concepts_line_still_follows_the_hint()
    {
        var text = IndexText.Status(new IndexStatusView([]) { Concepts = new IndexConceptsView(Enabled: false) });

        Assert.Equal("no record source is set up (fix: add one to the profile's context section)\nconcepts: off (context.conceptSynthesis or labeling is off)\n", text);
    }
}
