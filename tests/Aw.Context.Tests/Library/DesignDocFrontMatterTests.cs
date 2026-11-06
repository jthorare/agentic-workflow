using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

public sealed class DesignDocFrontMatterTests
{
    private static readonly string _full = string.Join('\n', "---", "aw-doc: 1                      # the convention version", "id: harbor-booking-tdd", "kind: tdd", "title: \"Harbor ferry: booking\"", "status: review", "project: linear:project/abc123", "repos: [acme/harbor-web, \"acme/harbor-api\"]", "issues: [412, ENG-12]", "supersedes: harbor-booking-v1", "parent: doc:repo:harbor-web/harbor-booking-plan", "tags:", "  - ferry", "---", "# Body");

    [Fact, Trait("Lane", "unit")]
    public void Every_key_is_read_and_keys_the_convention_does_not_name_are_ignored()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, _full);

        Assert.Equal(DesignDocParser.Version, doc.ParserVersion);
        Assert.Empty(doc.Errors);
        var fm = Assert.IsType<DesignDocFrontMatter>(doc.FrontMatter);
        Assert.Equal(1, fm.AwDoc);
        Assert.Equal("harbor-booking-tdd", fm.Slug);
        Assert.Equal("tdd", fm.Kind);
        Assert.Equal("Harbor ferry: booking", fm.Title);
        Assert.Equal("review", fm.Status);
        Assert.Equal("linear:project/abc123", fm.Project);
        Assert.Equal(["acme/harbor-web", "acme/harbor-api"], fm.Repos);
        Assert.Equal(["412", "ENG-12"], fm.Issues);
        Assert.Equal("harbor-booking-v1", fm.Supersedes);
        Assert.Equal("doc:repo:harbor-web/harbor-booking-plan", fm.Parent);
        Assert.Equal(3, fm.KeyLines["id"]);
        Assert.Equal(9, fm.KeyLines["issues"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_list_reads_like_a_flow_list()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, ADoc.Front(extra: "repos:\n  - acme/harbor-web\n  - acme/harbor-api\n") + "## Goals");

        Assert.Empty(doc.Errors);
        Assert.Equal(["acme/harbor-web", "acme/harbor-api"], doc.FrontMatter!.Repos);
    }

    [Fact, Trait("Lane", "unit")]
    public void Windows_line_endings_and_a_byte_order_mark_are_accepted()
    {
        var text = "﻿" + ADoc.Front().Replace("\n", "\r\n", StringComparison.Ordinal) + "## Goals\r\n";

        var doc = DesignDocParser.Parse(ADoc.Path, text);

        Assert.Empty(doc.Errors);
        Assert.Equal(["goals"], ADoc.Ids(doc));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("aw-doc: 2")]
    [InlineData("aw-doc: 0")]
    public void An_aw_doc_that_is_a_whole_number_other_than_1_is_a_bad_key_not_a_near_miss(string line)
    {
        var doc = DesignDocParser.Parse(ADoc.Path, $"---\n{line}\nid: harbor-booking\nkind: spec\ntitle: T\nstatus: draft\n---\n## Goals\n");

        Assert.Null(doc.FrontMatter);
        Assert.Empty(doc.Items);
        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.BadFrontMatter, error.Code);
        Assert.Equal(2, error.Line);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("aw-doc: \"1\"")]
    [InlineData("aw-doc: '1'")]
    [InlineData("aw-doc: 1.0")]
    [InlineData("aw-doc: yes")]
    [InlineData("aw-doc:")]
    public void An_aw_doc_that_is_not_the_number_1_is_a_near_miss_decided_on_the_raw_line(string line)
    {
        var doc = DesignDocParser.Parse(ADoc.Path, $"---\n{line}\nid: harbor-booking\nkind: spec\ntitle: T\nstatus: draft\n---\n## Goals\n");

        Assert.Null(doc.FrontMatter);
        Assert.Empty(doc.Items);
        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.NearMiss, error.Code);
        Assert.Equal(2, error.Line);
        Assert.Contains(ADoc.Path, error.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(error.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public void Aw_doc_1_after_a_blank_first_line_is_a_near_miss()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, "\n---\naw-doc: 1\nid: harbor-booking\nkind: spec\ntitle: T\nstatus: draft\n---\n## Goals\n");

        Assert.Null(doc.FrontMatter);
        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.NearMiss, error.Code);
        Assert.Equal(3, error.Line);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("# Just a note\n\n- **REQ-1** nothing reads this\n")]
    [InlineData("---\ntitle: An Obsidian note\ntags: [a]\n---\n## Goals\n")]
    [InlineData("")]
    public void A_file_without_aw_doc_front_matter_is_skipped_with_no_findings(string text)
    {
        var doc = DesignDocParser.Parse(ADoc.Path, text);

        Assert.Null(doc.FrontMatter);
        Assert.Empty(doc.Items);
        Assert.Empty(doc.Refs);
        Assert.Empty(doc.Errors);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("id: Bad_ID", "id")]
    [InlineData("id: ab", "id")]
    [InlineData("kind: wiki", "kind")]
    [InlineData("status: wip", "status")]
    [InlineData("title:", "title")]
    [InlineData("project: harbor", "project")]
    [InlineData("repos: acme/harbor-web", "repos")]
    [InlineData("repos: [harbor-web]", "repos")]
    [InlineData("issues: [\"412\"]", "issues")]
    [InlineData("issues: [eng-12]", "issues")]
    [InlineData("issues: [0]", "issues")]
    [InlineData("supersedes: Harbor_V1", "supersedes")]
    [InlineData("parent: doc:harbor", "parent")]
    [InlineData("generated: write_packet.py", "generated")]
    [InlineData("source-sha256: 3f2a", "source-sha256")]
    public void A_bad_front_matter_key_is_010_on_its_line_and_the_doc_keeps_no_items(string line, string key)
    {
        var key0 = key + ":";
        var lines = "aw-doc: 1\nid: harbor-booking\nkind: spec\ntitle: T\nstatus: draft\n".Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => !l.StartsWith(key0, StringComparison.Ordinal)).Append(line).ToArray();
        var text = "---\n" + string.Join('\n', lines) + "\n---\n## Goals\n";

        var doc = DesignDocParser.Parse(ADoc.Path, text);

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.BadFrontMatter, error.Code);
        Assert.Equal(lines.Length + 1, error.Line);
        Assert.Empty(doc.Items);
        Assert.NotNull(doc.FrontMatter);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_missing_required_key_is_010()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, "---\naw-doc: 1\nid: harbor-booking\nkind: spec\nstatus: draft\n---\n## Goals\n");

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.BadFrontMatter, error.Code);
        Assert.Contains("title", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Front_matter_that_never_closes_is_010()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, "---\naw-doc: 1\nid: harbor-booking\nkind: spec\ntitle: T\nstatus: draft\n## Goals\n");

        Assert.Equal([DesignDocCodes.BadFrontMatter], ADoc.Codes(doc));
        Assert.Empty(doc.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_generated_doc_whose_source_hash_no_longer_matches_is_017()
    {
        var sha = new string('a', 64);
        var text = ADoc.Front(extra: $"generated: write_packet.py\nsource-sha256: {sha}\n") + "## Goals\n";

        var stale = DesignDocParser.Parse(ADoc.Path, text, new string('b', 64));
        var fresh = DesignDocParser.Parse(ADoc.Path, text, sha.ToUpperInvariant());
        var unknown = DesignDocParser.Parse(ADoc.Path, text);

        var error = Assert.Single(stale.Errors);
        Assert.Equal(DesignDocCodes.StaleGenerated, error.Code);
        Assert.Equal(ADoc.Body + 1, error.Line);
        Assert.Empty(stale.Items);
        Assert.Empty(fresh.Errors);
        Assert.Empty(unknown.Errors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Over_a_mebibyte_is_018_with_no_line_and_no_items()
    {
        var body = "## Goals\n" + new string('x', DesignDocParser.MaxBytes);

        var doc = ADoc.Parse("spec", body);

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.TooLarge, error.Code);
        Assert.Null(error.Line);
        Assert.Empty(doc.Items);
        Assert.NotNull(doc.FrontMatter);
    }

    [Fact, Trait("Lane", "unit")]
    public void More_than_2000_items_is_018()
    {
        var ok = ADoc.Parse("spec", string.Join('\n', Enumerable.Range(1, DesignDocParser.MaxItems).Select(static n => "## Section " + n.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        var tooMany = ADoc.Parse("spec", string.Join('\n', Enumerable.Range(1, DesignDocParser.MaxItems + 1).Select(static n => "## Section " + n.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        Assert.Empty(ok.Errors);
        Assert.Equal(DesignDocParser.MaxItems, ok.Items.Count);
        Assert.Equal([DesignDocCodes.TooLarge], ADoc.Codes(tooMany));
        Assert.Empty(tooMany.Items);
    }
}
