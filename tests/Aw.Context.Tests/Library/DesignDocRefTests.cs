using Aw.Context.Core.Library;

namespace Aw.Context.Tests.Library;

public sealed class DesignDocRefTests
{
    private const int B = ADoc.Body;

    [Fact, Trait("Lane", "unit")]
    public void A_test_plan_rows_other_cells_that_hold_ids_verify_them()
    {
        var Body = string.Join('\n', "## Proof", "| Row | Check | Verifies |", "|---|---|---|", "| TP-1 | Book a seat on the fake ferry; it shows booked. | REQ-1 |", "| TP-2 | Fail the payment; the seat shows free. Verifies REQ-E2, AC-1 | Ph2 |");

        var doc = ADoc.Parse("tdd", Body);

        Assert.Empty(doc.Errors);
        Assert.Equal(["proof", "TP-1", "TP-2"], ADoc.Ids(doc));
        Assert.Equal(DesignItemKind.TestPlan, doc.Items[1].Kind);
        Assert.Equal(
            [
                new DesignRef("TP-1", "REQ-1", DesignRefKind.Verifies, B + 4),
                new DesignRef("TP-2", "REQ-E2", DesignRefKind.Verifies, B + 5),
                new DesignRef("TP-2", "AC-1", DesignRefKind.Verifies, B + 5),
                new DesignRef("TP-2", "Ph2", DesignRefKind.Verifies, B + 5),
            ],
            doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tasks_reqs_and_depends_list_items_become_relates_and_blocks_refs()
    {
        var Body = string.Join('\n', "## TASK-1 Booking form", "- reqs: REQ-U1", "", "## TASK-2 Payment", "- reqs: REQ-E3, harbor-booking-product#REQ-U1", "- depends: TASK-1");

        var doc = ADoc.Parse("tasks", Body, "harbor-booking-tasks");

        Assert.Empty(doc.Errors);
        Assert.Equal(["TASK-1", "TASK-2"], ADoc.Ids(doc));
        Assert.All(doc.Items, static i => Assert.Equal(DesignItemKind.Task, i.Kind));
        Assert.Equal((B + 1, B + 2), (doc.Items[0].LineStart, doc.Items[0].LineEnd));
        Assert.Equal("## TASK-1 Booking form\n- reqs: REQ-U1", doc.Items[0].Text);
        Assert.Equal(
            [
                new DesignRef("TASK-1", "REQ-U1", DesignRefKind.Relates, B + 2),
                new DesignRef("TASK-2", "REQ-E3", DesignRefKind.Relates, B + 5),
                new DesignRef("TASK-2", "harbor-booking-product#REQ-U1", DesignRefKind.Relates, B + 5),
                new DesignRef("TASK-2", "TASK-1", DesignRefKind.Blocks, B + 6),
            ],
            doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_full_doc_ref_and_a_Ph01_ref_are_kept_and_normalized()
    {
        var doc = ADoc.Parse("tasks", "## TASK-1 Form\n- reqs: doc:aw/acme-harbor/harbor-booking-product#REQ-U1\n- depends: harbor-booking-stages#Ph01\n", "harbor-booking-tasks");

        Assert.Equal(
            ["doc:aw/acme-harbor/harbor-booking-product#REQ-U1", "harbor-booking-stages#Ph1"],
            [.. doc.Refs.Select(static r => r.To)]);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_id_in_a_doc_kind_that_does_not_define_it_is_a_ref_from_the_item_it_sits_under()
    {
        var doc = ADoc.Parse("tasks", "## TASK-1 Build\n- **REQ-U1** the rider books\n- AC-1 shows booked\n", "harbor-booking-tasks");

        Assert.Empty(doc.Errors);
        Assert.Equal(["TASK-1"], ADoc.Ids(doc));
        Assert.Equal(
            [new DesignRef("TASK-1", "REQ-U1", DesignRefKind.Relates, B + 2), new DesignRef("TASK-1", "AC-1", DesignRefKind.Relates, B + 3)],
            doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_row_whose_first_cell_is_not_defined_here_links_to_the_ids_in_its_other_cells()
    {
        var doc = ADoc.Parse("engineering", "## Trace\n| REQ-U1 | AC-1, AC-2 | notes |\n", "harbor-booking-eng");

        Assert.Equal(
            [new DesignRef("REQ-U1", "AC-1", DesignRefKind.Relates, B + 2), new DesignRef("REQ-U1", "AC-2", DesignRefKind.Relates, B + 2)],
            doc.Refs);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_doc_with_an_error_keeps_no_refs()
    {
        var doc = ADoc.Parse("tdd", "## Proof\n| TP-1 | x | REQ-1 |\n- **REQ-** bad\n");

        Assert.Equal([DesignDocCodes.BadItemId], ADoc.Codes(doc));
        Assert.Empty(doc.Refs);
    }
}
