using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

public sealed class DesignDocItemTests
{
    private const int B = ADoc.Body;

    [Fact, Trait("Lane", "unit")]
    public void Headings_two_to_four_are_sections_by_their_anchor_with_the_section_above_as_parent()
    {
        var doc = ADoc.Parse("note", "# Harbor\n## Goals\n### Booking form\n#### Seats\n##### Too deep\n## Goals\n");

        Assert.Empty(doc.Errors);
        Assert.Equal(["goals", "booking-form", "seats", "goals-1"], ADoc.Ids(doc));
        Assert.All(doc.Items, static i => Assert.Equal(DesignItemKind.Section, i.Kind));
        Assert.Equal([null, "goals", "booking-form", null], [.. doc.Items.Select(static i => i.Parent)]);
        Assert.Equal([(B + 2, B + 5), (B + 3, B + 5), (B + 4, B + 5), (B + 6, B + 6)], doc.Items.Select(static i => (i.LineStart, i.LineEnd)));
    }

    [Fact, Trait("Lane", "unit")]
    public void Anchors_follow_githubs_slug_rule_and_repeats_count_every_heading()
    {
        var doc = ADoc.Parse("note", "# Data model\n## 12. Phases and parallel plan (for /specToProvenPR)\n## Data model\n## Data model\n## **Bold** `code` [link](https://example.test) — dash\n");

        Assert.Equal(
            ["12-phases-and-parallel-plan-for-spectoprovenpr", "data-model-1", "data-model-2", "bold-code-link--dash"],
            ADoc.Ids(doc));
    }

    [Fact, Trait("Lane", "unit")]
    public void Nothing_in_a_fence_or_a_quote_is_read()
    {
        var Body = string.Join('\n', "## Real", "```text", "## Fenced", "- **REQ-9** fenced", "```", "~~~", "## Tilde", "~~~", "> ## Quoted", "> - **REQ-8** quoted", "> [!note]", "- **REQ-1** real");

        var doc = ADoc.Parse("spec", Body);

        Assert.Empty(doc.Errors);
        Assert.Equal(["real", "REQ-1"], ADoc.Ids(doc));
        Assert.Equal(B + 12, doc.Items[0].LineEnd);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bold_lead_in_works_inside_a_list_item_but_a_bare_paragraph_is_not_an_item()
    {
        var doc = ADoc.Parse("spec", "## Goals\n**REQ-3** a bare paragraph\n- **REQ-4** bold list item\n1. REQ-5 numbered\n* REQ-6 starred\n");

        Assert.Equal(["goals", "REQ-4", "REQ-5", "REQ-6"], ADoc.Ids(doc));
        Assert.All(doc.Items.Skip(1), static i => Assert.Equal(DesignItemKind.Requirement, i.Kind));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_table_rows_first_cell_is_the_item_and_the_header_and_separator_rows_are_not()
    {
        var doc = ADoc.Parse("spec", "## Phases\n| Phase | What |\n|---|---|\n| **Ph0** | The `a|b` page |\n| Ph3b | later |\n");

        Assert.Empty(doc.Errors);
        Assert.Equal(["phases", "Ph0", "Ph3b"], ADoc.Ids(doc));
        var ph0 = doc.Items[1];
        Assert.Equal(DesignItemKind.Phase, ph0.Kind);
        Assert.Equal("phases", ph0.Parent);
        Assert.Equal((B + 4, B + 4), (ph0.LineStart, ph0.LineEnd));
        Assert.StartsWith("| **Ph0**", ph0.Text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Phase_ids_take_a_letter_or_a_number_and_Ph01_reads_as_Ph1()
    {
        var doc = ADoc.Parse("spec", "## Phases\n### Ph0 Convention\n### Ph3b Later\n### Ph2-1 Sub\n### Ph01 Zero\n");

        Assert.Empty(doc.Errors);
        Assert.Equal(["phases", "Ph0", "Ph3b", "Ph2-1", "Ph1"], ADoc.Ids(doc));
        Assert.All(doc.Items.Skip(1), static i => Assert.Equal("phases", i.Parent));
    }

    [Fact, Trait("Lane", "unit")]
    public void Ph01_and_Ph1_define_one_id_twice()
    {
        var doc = ADoc.Parse("spec", "### Ph1 A\n### Ph01 B\n");

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.DuplicateItem, error.Code);
        Assert.Equal(B + 2, error.Line);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("tasks", "## Phases\n### Ph1 Form\n- **REQ-1** x\n- AC-1 y\n- TP-1 z\n", "phases,ph1-form")]
    [InlineData("engineering", "## TASK-1 Build\n- **REQ-1** x\n", "task-1-build")]
    [InlineData("spec", "## TASK-1 Build\n", "task-1-build")]
    [InlineData("note", "- **REQ-1** x\n- Ph1 y\n| TP-1 | z |\n", "")]
    public void An_id_is_defined_only_in_its_kinds_elsewhere_its_line_is_not_an_item(string kind, string body, string sectionIds)
    {
        var doc = ADoc.Parse(kind, body);

        Assert.Empty(doc.Errors);
        var expected = sectionIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected, ADoc.Ids(doc));
        Assert.All(doc.Items, static i => Assert.Equal(DesignItemKind.Section, i.Kind));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("spec", "REQ-1,AC-1,TP-1,Ph1")]
    [InlineData("tdd", "REQ-1,AC-1,TP-1,Ph1")]
    [InlineData("product", "REQ-1,AC-1")]
    [InlineData("stages", "Ph1")]
    [InlineData("verification", "TP-1")]
    public void The_kinds_table_decides_what_each_doc_kind_defines(string kind, string defined)
    {
        var doc = ADoc.Parse(kind, "- **REQ-1** a\n- AC-1 b\n- TP-1 c\n- Ph1 d\n");

        Assert.Empty(doc.Errors);
        Assert.Equal(defined.Split(','), ADoc.Ids(doc));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_items_text_is_its_line_and_the_lines_under_it_up_to_the_next_item_of_its_level_or_higher()
    {
        var doc = ADoc.Parse("spec", "## Goals\n- **REQ-E2** When payment fails,\n  the seat is released.\n  - AC-1 free within 10 s\n- **REQ-1** next\n\n## Next\n");

        Assert.Empty(doc.Errors);
        var req = doc.Items.Single(static i => i.Key == "REQ-E2");
        Assert.Equal((B + 2, B + 4), (req.LineStart, req.LineEnd));
        Assert.Equal("- **REQ-E2** When payment fails,\n  the seat is released.\n  - AC-1 free within 10 s", req.Text);
        var goals = doc.Items[0];
        Assert.Equal(B + 5, goals.LineEnd);
        var ac = doc.Items.Single(static i => i.Key == "AC-1");
        Assert.Equal((B + 4, B + 4), (ac.LineStart, ac.LineEnd));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_items_text_is_capped_at_4000_characters()
    {
        var doc = ADoc.Parse("spec", "## Goals\n" + string.Join('\n', Enumerable.Repeat(new string('x', 99), 80)) + "\n");

        var goals = Assert.Single(doc.Items);
        Assert.Equal(DesignDocParser.MaxTextChars, goals.Text.Length);
        Assert.Equal(B + 81, goals.LineEnd);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_definition_wins_and_a_traceability_row_repeating_it_links_the_two()
    {
        var Body = string.Join('\n', "## Requirements", "- **REQ-U1** a rider books a seat", "- **REQ-U2** a rider cancels", "## Criteria", "- AC-1 the seat shows booked", "- AC-2 the seat shows free", "## Traceability", "| Req | AC |", "|---|---|", "| REQ-U1 | AC-1 |", "| REQ-U2 | AC-2 |");

        var doc = ADoc.Parse("spec", Body);

        Assert.Empty(doc.Errors);
        Assert.Equal(["requirements", "REQ-U1", "REQ-U2", "criteria", "AC-1", "AC-2", "traceability"], ADoc.Ids(doc));
        Assert.Equal("REQ-U1", doc.Items.Single(static i => i.Key == "AC-1").Parent);
        Assert.Equal("REQ-U2", doc.Items.Single(static i => i.Key == "AC-2").Parent);
        Assert.Equal(
            [new DesignRef("REQ-U1", "AC-1", DesignRefKind.Relates, B + 10), new DesignRef("REQ-U2", "AC-2", DesignRefKind.Relates, B + 11)],
            doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_list_item_repeating_an_id_a_table_defined_is_a_reference_not_a_second_definition()
    {
        var doc = ADoc.Parse("spec", "## Phases\n| **Ph1** | first |\n## Notes\n- **Ph1**: store claims\n");

        Assert.Empty(doc.Errors);
        Assert.Equal(["phases", "Ph1", "notes"], ADoc.Ids(doc));
        Assert.Equal([new DesignRef("notes", "Ph1", DesignRefKind.Relates, B + 4)], doc.Refs);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("### Ph1 A\n### Ph1 B\n", 2)]
    [InlineData("- **REQ-3** a\n- **REQ-3** again\n", 2)]
    [InlineData("## Goals\n- **REQ-3** a\n\n- **REQ-3** again\n", 4)]
    public void Two_headings_or_two_list_items_defining_one_id_are_013_at_the_second(string body, int line)
    {
        var doc = ADoc.Parse("spec", body);

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.DuplicateItem, error.Code);
        Assert.Equal(B + line, error.Line);
        Assert.Contains((B + line).ToString(System.Globalization.CultureInfo.InvariantCulture), error.Message, StringComparison.Ordinal);
        Assert.Empty(doc.Items);
        Assert.Empty(doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_acceptance_criterion_belongs_to_the_nearest_requirement_above_it_in_its_section()
    {
        var doc = ADoc.Parse("spec", "## Goals\n- **REQ-E2** release\n  - AC-1 free\n- **REQ-1** other\n- AC-2 under req 1\n");

        Assert.Empty(doc.Errors);
        Assert.Equal("REQ-E2", doc.Items.Single(static i => i.Key == "AC-1").Parent);
        Assert.Equal("REQ-1", doc.Items.Single(static i => i.Key == "AC-2").Parent);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_table_row_pairing_beats_the_requirement_above()
    {
        var doc = ADoc.Parse("spec", "## Goals\n- **REQ-1** a\n- **REQ-2** b\n- AC-1 c\n## Trace\n| REQ-2 | AC-1 |\n");

        Assert.Empty(doc.Errors);
        Assert.Equal("REQ-2", doc.Items.Single(static i => i.Key == "AC-1").Parent);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("## Criteria\n- AC-1 lonely\n", 2)]
    [InlineData("## A\n- **REQ-1** a\n## B\n- AC-1 other section\n", 4)]
    public void An_acceptance_criterion_with_no_pair_and_no_requirement_in_its_section_is_014(string body, int line)
    {
        var doc = ADoc.Parse("spec", body);

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.OrphanAcceptance, error.Code);
        Assert.Equal(B + line, error.Line);
        Assert.Empty(doc.Items);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("- **REQ-** no number\n", 1)]
    [InlineData("- **REQ-X3** bad letter\n", 1)]
    [InlineData("## Phases\n### Ph1ab Bad\n", 2)]
    [InlineData("| TP- | z |\n", 1)]
    public void An_item_id_of_a_bad_shape_is_011(string body, int line)
    {
        var doc = ADoc.Parse("spec", body);

        var error = Assert.Single(doc.Errors);
        Assert.Equal(DesignDocCodes.BadItemId, error.Code);
        Assert.Equal(B + line, error.Line);
        Assert.Empty(doc.Items);
    }

    [Fact, Trait("Lane", "unit")]
    public void Words_that_only_start_like_an_id_are_not_item_ids()
    {
        var doc = ADoc.Parse("spec", "## Phases\n- Phase two of the plan\n- Action items\n- TR-4 stays plain text\n- ADR-2 too\n");

        Assert.Empty(doc.Errors);
        Assert.Equal(["phases"], ADoc.Ids(doc));
    }
}
