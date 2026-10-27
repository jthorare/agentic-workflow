using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Tests.Extract;

public sealed class DocMentionsTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("app");
    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-20T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static IReadOnlyList<GraphNode> Records() =>
        DocRecords.Find(_repo, DocRecordsTests.Paths, ContextSettings.DefaultDocGlobs, ContextSettings.DefaultDocDirectoryNames, _at);

    private static Dictionary<string, string?> Texts() => new(StringComparer.Ordinal)
    {
        ["src/billing/AGENTS.md"] = "Entry: `src/billing/Invoice.cs` and src/billing/Tax.cs:12. Again `src/billing/Invoice.cs`.",
        ["src/links/AGENTS.md"] = "See `src/links/Share.cs` and `src/links/Token.cs:40`. Old: `src/links/Gone.cs`.",
        ["docs/guide.md"] = "Nothing cited here.",
    };

    private static DocMentionScan Scan(Dictionary<string, string?>? texts = null) => DocMentions.Find(_repo, Records(), texts ?? Texts(), DocRecordsTests.Paths);

    [Fact, Trait("Lane", "unit")]
    public void Each_doc_mentions_exactly_the_files_it_cites_once()
    {
        var scan = Scan();

        Assert.Equal(
            [
                "record:repo:app/src/billing/AGENTS.md>code:src/billing/Invoice.cs",
                "record:repo:app/src/billing/AGENTS.md>code:src/billing/Tax.cs",
                "record:repo:app/src/links/AGENTS.md>code:src/links/Share.cs",
                "record:repo:app/src/links/AGENTS.md>code:src/links/Token.cs",
            ],
            scan.Edges.Select(e => e.From.Value + ">" + e.To.Value), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_edge_is_a_deterministic_mentions_of_confidence_1_that_rests_on_the_doc()
    {
        var edge = Scan().Edges[0];

        Assert.Equal((EdgeType.Mentions, Provenance.Deterministic, 1.0), (edge.Type, edge.Provenance, edge.Confidence));
        Assert.Equal(RepoDocs.Key(_repo, "src/billing/AGENTS.md"), Assert.Single(edge.EvidenceOrEmpty).Key);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_path_that_is_not_in_the_repo_is_counted_and_never_an_edge()
    {
        var scan = Scan();

        Assert.Equal((1, 1), (scan.DocsWithMissing, scan.MissingPaths));
        Assert.DoesNotContain(scan.Edges, e => e.To.Value.Contains("Gone.cs", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_doc_whose_text_was_withheld_keeps_its_record_and_has_no_edges()
    {
        var texts = Texts();
        texts["src/links/AGENTS.md"] = null;

        var scan = Scan(texts);

        Assert.Contains(Records(), r => r.Key.Value == "record:repo:app/src/links/AGENTS.md");
        Assert.DoesNotContain(scan.Edges, e => e.From.Value == "record:repo:app/src/links/AGENTS.md");
        Assert.Equal((0, 0), (scan.DocsWithMissing, scan.MissingPaths));
    }

    [Fact, Trait("Lane", "unit")]
    public void Text_for_a_path_with_no_record_is_ignored_and_running_twice_gives_the_same_scan()
    {
        var texts = Texts();
        texts["notes/random.md"] = "`src/billing/Tax.cs`";

        var first = Scan(texts);
        var second = Scan(texts);

        Assert.Equal(4, first.Edges.Count);
        Assert.Equal(first.Edges, second.Edges);
        Assert.Equal((first.DocsWithMissing, first.MissingPaths), (second.DocsWithMissing, second.MissingPaths));
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_docs_citing_the_same_missing_path_are_two_docs_and_one_path()
    {
        var texts = Texts();
        texts["docs/guide.md"] = "Also `src/links/Gone.cs`.";

        var scan = Scan(texts);

        Assert.Equal((2, 1), (scan.DocsWithMissing, scan.MissingPaths));
    }
}
