using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

/// <summary>One test per valid example in <c language="text">schemas/library/examples</c>: made-up inputs whose parse has the example's key facts.</summary>
public sealed class DesignDocExampleTests
{
    private const int B = ADoc.Body;

    [Fact, Trait("Lane", "unit")]
    public void Spec_items_a_requirement_a_phase_and_a_test_row_with_their_sections_and_verifies_refs()
    {
        var Body = string.Join('\n', "## 2. Goals and non-goals", "- **REQ-7** Front-matter docs are indexed and itemized.", "## 12. Phases and parallel plan (for /specToProvenPR)", "| Phase | What |", "|---|---|", "| **Ph0** | — | ideas | The convention page ... |", "## 13. Proof", "| **TP-1** | The fixture vault is itemized. Verifies REQ-7, REQ-12 |");

        var doc = ADoc.Parse("spec", Body);

        Assert.Equal(DesignDocParser.Version, doc.ParserVersion);
        Assert.Empty(doc.Errors);
        Assert.Equal(["2-goals-and-non-goals", "REQ-7", "12-phases-and-parallel-plan-for-spectoprovenpr", "Ph0", "13-proof", "TP-1"], ADoc.Ids(doc));
        Assert.Equal(
            [DesignItemKind.Section, DesignItemKind.Requirement, DesignItemKind.Section, DesignItemKind.Phase, DesignItemKind.Section, DesignItemKind.TestPlan],
            doc.Items.Select(static i => i.Kind));
        Assert.Equal([null, "2-goals-and-non-goals", null, "12-phases-and-parallel-plan-for-spectoprovenpr", null, "13-proof"], [.. doc.Items.Select(static i => i.Parent)]);
        Assert.Equal(
            [new DesignRef("TP-1", "REQ-7", DesignRefKind.Verifies, B + 8), new DesignRef("TP-1", "REQ-12", DesignRefKind.Verifies, B + 8)],
            doc.Refs);
        Assert.Equal("spec", doc.FrontMatter!.Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Tasks_refs_a_task_per_heading_with_relates_and_blocks()
    {
        var doc = ADoc.Parse("tasks", "## TASK-1 Booking form\n- reqs: REQ-U1\n\n## TASK-2 Payment\n- reqs: REQ-E3\n- depends: TASK-1\n", "harbor-booking-tasks");

        Assert.Equal(["TASK-1", "TASK-2"], ADoc.Ids(doc));
        Assert.All(doc.Items, static i => Assert.Null(i.Parent));
        Assert.Equal(
            [DesignRefKind.Relates, DesignRefKind.Relates, DesignRefKind.Blocks],
            doc.Refs.Select(static r => r.Kind));
        Assert.Equal(["TASK-1", "TASK-2", "TASK-2"], [.. doc.Refs.Select(static r => r.From)]);
        Assert.Empty(doc.Errors);
    }

    [Fact, Trait("Lane", "unit")]
    public void Generated_ac_parent_a_generated_doc_with_a_full_supersedes_ref_an_ac_and_a_Ph2_1_heading()
    {
        var sha = new string('3', 64);
        var text = ADoc.Front("tdd", "harbor-booking-design", $"project: prj_01JABCDEF\ngenerated: write_packet.py\nsource-sha256: {sha}\nsupersedes: doc:aw/acme-harbor/harbor-booking-v1\n")
            + "- **REQ-E3** When payment fails, the seat is released.\n- AC-1 The seat shows free within 10 s.\n## Ph2-1 Payments\n";

        var doc = DesignDocParser.Parse(ADoc.Path, text, sha);

        Assert.Empty(doc.Errors);
        var fm = doc.FrontMatter!;
        Assert.Equal("write_packet.py", fm.Generated);
        Assert.Equal(sha, fm.SourceSha256);
        Assert.Equal("doc:aw/acme-harbor/harbor-booking-v1", fm.Supersedes);
        Assert.Equal("prj_01JABCDEF", fm.Project);
        Assert.Equal(["REQ-E3", "AC-1", "Ph2-1"], ADoc.Ids(doc));
        Assert.Equal([null, "REQ-E3", null], [.. doc.Items.Select(static i => i.Parent)]);
        Assert.Empty(doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void Broken_no_items_a_duplicate_requirement_is_one_013_naming_both_lines_and_no_items()
    {
        var doc = ADoc.Parse("spec", "## Goals\n- **REQ-3** first\n\n\n- **REQ-3** second\n");

        Assert.NotNull(doc.FrontMatter);
        Assert.Empty(doc.Items);
        Assert.Empty(doc.Refs);
        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.DuplicateItem, error.Code);
        Assert.Equal(B + 5, error.Line);
        Assert.Contains("REQ-3", error.Message, StringComparison.Ordinal);
        Assert.Contains((B + 2).ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Contains((B + 5).ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(error.Fix));
    }

    [Fact, Trait("Lane", "unit")]
    public void Near_miss_no_front_matter_a_quoted_aw_doc_has_null_front_matter_and_one_019()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, "---\naw-doc: \"1\"\nid: harbor-booking\nkind: spec\ntitle: T\nstatus: draft\n---\n## Goals\n");

        Assert.Null(doc.FrontMatter);
        Assert.Empty(doc.Items);
        Assert.Empty(doc.Refs);
        var error = Assert.Single(doc.Errors);
        Assert.Equal((DesignDocCodes.NearMiss, 2), (error.Code, error.Line));
    }

    [Fact, Trait("Lane", "unit")]
    public void Bad_id_reported_a_bad_doc_id_is_one_010_and_the_front_matter_is_kept_as_written()
    {
        var doc = DesignDocParser.Parse(ADoc.Path, ADoc.Front(id: "Bad_ID") + "## Goals\n");

        Assert.Equal("Bad_ID", doc.FrontMatter!.Slug);
        Assert.Empty(doc.Items);
        var error = Assert.Single(doc.Errors);
        Assert.Equal((DesignDocCodes.BadFrontMatter, 3), (error.Code, error.Line));
        Assert.Contains("Bad_ID", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Broken_ref_keeps_items_the_scope_step_adds_015_and_the_docs_items_and_refs_stay()
    {
        var spec = new ScopedDesignDoc(
            "/aw/spec.md",
            "aw/acme-harbor",
            DesignDocParser.Parse("/aw/spec.md", ADoc.Front() + "## Goals\n- **REQ-7** x\n## Proof\n| **TP-1** | Verifies REQ-7, REQ-12 |\n"));

        var findings = DesignDocScope.Check([spec]);

        var finding = Assert.Single(findings);
        Assert.Equal(DesignDocCodes.BrokenRef, finding.Error.Code);
        Assert.Equal(B + 4, finding.Error.Line);
        Assert.Contains("REQ-12", finding.Error.Message, StringComparison.Ordinal);
        Assert.Equal(["goals", "REQ-7", "proof", "TP-1"], ADoc.Ids(spec.Doc));
        Assert.Equal(2, spec.Doc.Refs.Count);
    }
}
