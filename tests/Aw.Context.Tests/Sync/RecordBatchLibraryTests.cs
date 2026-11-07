using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Context.Core.Trust;
using Aw.Context.Testing.Engine;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Library;

namespace Aw.Context.Tests.Sync;

/// <summary>What <see cref="RecordBatches"/> hands the store for the design-doc library (spec 2026-11-04 §4.4, §5.3): a parse for doc records of the doc feeds, claims for PR records.</summary>
[Trait("Lane", "unit")]
public sealed class RecordBatchLibraryTests
{
    private const string Spec = "---\naw-doc: 1\nid: harbor\nkind: spec\ntitle: \"Harbor\"\nstatus: draft\n---\n## Goals\n- **REQ-1** Book a ferry.\n";

    private readonly Scrubbers _scrubbers = new FakeScrubberFactory().For([]).Value;

    private RecordWrite Build(string source, RecordKind kind, string key, string text)
    {
        var item = new RecordItem(
            RecordKey.From(key), kind, "Title", text, Author: null, Url: null, State: null, StateRaw: null,
            CreatedAt: null, UpdatedAt: null, ClosedAt: null, SourceVersion.From("v1"), [], Repo: null, Audience: null);
        var page = new RecordPage([item], [], SourceCursor.From("c"), CursorReset: false);
        return RecordBatches.From(SourceId.From(source), page, SyncPhase.Incremental, _scrubbers, RecordSamples.Now).Records.Single();
    }

    private static ParsedDesignDoc? Doc(RecordWrite write) => (write.Side as DesignDocSideRows)?.Doc;

    private static IReadOnlyList<string>? Claims(RecordWrite write) => (write.Side as PrClaimSideRows)?.Refs;

    [Theory]
    [InlineData("repo-docs", "repo:aw/docs/a.md")]
    [InlineData("library", "doc:work/a.md")]
    public void A_design_doc_of_a_doc_feed_gets_its_parse_over_the_cleaned_text(string source, string key)
    {
        var write = Build(source, RecordKind.Doc, key, Spec);

        Assert.Equal(["goals", "REQ-1"], ADoc.Ids(Doc(write)!));
        Assert.Equal("harbor", Doc(write)!.FrontMatter!.Slug);
        Assert.Empty(Doc(write)!.Errors);
        Assert.Null(Claims(write));
    }

    [Fact]
    public void A_doc_without_aw_doc_front_matter_gets_no_parse()
    {
        Assert.Null(Doc(Build("repo-docs", RecordKind.Doc, "repo:aw/docs/a.md", "# Alpha\n\nNo front matter.\n")));
        Assert.Null(Doc(Build("repo-docs", RecordKind.Doc, "repo:aw/docs/b.md", "---\ntitle: x\n---\nBody\n")));
    }

    [Fact]
    public void A_doc_the_scrubber_blocks_is_blocked_with_no_items()
    {
        var write = Build("repo-docs", RecordKind.Doc, "repo:aw/docs/a.md", Spec + "- **REQ-2** " + FakeScrubber.BlockMe + "\n");

        Assert.True(write.Record.Blocked);
        Assert.Empty(Doc(write)!.Items);
        Assert.Empty(write.Chunks);
        Assert.Contains("blocked", Assert.Single(Doc(write)!.Errors).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blocked_file_that_is_not_a_design_doc_gets_no_parse() =>
        Assert.Null(Doc(Build("repo-docs", RecordKind.Doc, "repo:aw/docs/a.md", "# Plain " + FakeScrubber.BlockMe)));

    [Fact]
    public void A_doc_of_another_source_is_not_itemized_and_keeps_the_plain_ruleset()
    {
        var write = Build("linear", RecordKind.Doc, "linear:doc/1", Spec);

        Assert.Null(write.Side);
        Assert.Equal(_scrubbers.Ruleset.Value, write.Record.Ruleset);
    }

    [Fact]
    public void An_itemized_docs_ruleset_carries_the_parser_version()
    {
        var write = Build("repo-docs", RecordKind.Doc, "repo:aw/docs/a.md", "# Plain\n");

        Assert.Equal(DesignDocParser.Stamp(_scrubbers.Ruleset.Value), write.Record.Ruleset);
        Assert.EndsWith("p" + DesignDocParser.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), write.Record.Ruleset, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Summary\n\nCovers: harbor#Ph2, `harbor#REQ-1`\n", new[] { "harbor#Ph2", "harbor#REQ-1" })]
    [InlineData("covers: harbor#Ph2", new[] { "harbor#Ph2" })]
    [InlineData("Covers: none (a typo fix)", new string[0])]
    [InlineData("Covers: a#Ph1\nCovers: b#Ph2", new string[0])]
    [InlineData("No claims here", new string[0])]
    public void A_pull_requests_covers_line_becomes_its_claims(string body, string[] expected)
    {
        var write = Build("github", RecordKind.Pr, "github:pr/acme/widgets/12", body);

        Assert.Equal(expected, Claims(write));
        Assert.Null(Doc(write));
    }

    [Fact]
    public void A_blocked_pull_request_claims_nothing()
    {
        var write = Build("github", RecordKind.Pr, "github:pr/acme/widgets/12", "Covers: harbor#Ph2 " + FakeScrubber.BlockMe);

        Assert.Empty(Claims(write)!);
    }

    [Fact]
    public void An_issue_carries_neither_a_parse_nor_claims()
    {
        var write = Build("github", RecordKind.Issue, "github:issue/acme/widgets/3", "Covers: harbor#Ph2");

        Assert.Null(Claims(write));
        Assert.Null(Doc(write));
    }
}
