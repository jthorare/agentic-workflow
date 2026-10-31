using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Pages;

namespace Aw.Context.Tests.Pages;

/// <summary>A pasted page list previewed against the map's pages: which blocks match, which name the same page twice, which the save would add.</summary>
public sealed class PageListPreviewTests
{
    private const string Week = "surface:route/GET:/schedule/week";

    private static readonly (ConceptId Id, string Name)[] _concepts =
    [
        (ConceptId.From("self-scheduling"), "Self-scheduling"), (ConceptId.From("approval-workflow"), "Approval workflow"),
    ];

    private static PageRow ScheduleWeek() => Page("schedule-week", "Schedule week", Week, "suggested",
        Action("submit", "Submit schedule", "self-scheduling"), Action("cancel", "Cancel schedule"), Action("approve", "Approve"), Action("reopen", "Reopen week"));

    private static PageRow Page(string id, string name, string key, string mark, params PageActionRow[] actions) =>
        new(PageId.From(id), NodeKey.From(key), name, Repo: "aw-split-panes", Mark: mark, Code: true, Actions: actions);

    private static PageActionRow Action(string id, string name, params string[] concepts) =>
        new(PageActionId.From(id), NodeKey.From($"action:WeekPage.{id}"), name, "suggested", Code: true, Concepts: concepts);

    private static IReadOnlyList<PageListRow> Preview(string paste, params PageRow[] pages) =>
        PageListPreview.Of(PageListText.Parse(paste), pages, _concepts);

    [Fact, Trait("Lane", "unit")]
    public void Two_blocks_naming_the_same_page_flag_the_second()
    {
        var rows = Preview("Schedule week\nSubmit schedule\n\n- Schedule weeks\n- Approve", ScheduleWeek());

        Assert.Equal(2, rows.Count);
        Assert.Equal(Week, rows[0].Match?.Key.Value);
        Assert.True(rows[0].Ticked);
        Assert.Null(rows[0].SameAs);
        Assert.Equal(Week, rows[1].Match?.Key.Value);
        Assert.Equal(1, rows[1].SameAs);
        Assert.False(rows[1].Ticked);
        Assert.Equal("Not added: Same page as block 1", rows[1].Reason);
        Assert.Null(rows[1].Page);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_whose_words_equal_a_page_beats_one_that_only_shares_words()
    {
        var longer = Page("schedule-week-approvals", "Schedule week approvals", "surface:route/GET:/schedule/approvals", "suggested");

        var rows = Preview("Schedule week", longer, ScheduleWeek());

        Assert.Equal(Week, rows[0].Match?.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void With_no_equal_page_the_block_takes_the_page_sharing_the_most_words()
    {
        var one = Page("schedule", "Schedule", "surface:screen/Schedule", "suggested");
        var two = Page("schedule-week-approvals", "Schedule week approvals", "surface:route/GET:/schedule/approvals", "suggested");

        var rows = Preview("Schedule week", one, two);

        Assert.Equal("surface:route/GET:/schedule/approvals", rows[0].Match?.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void Equal_pages_tie_on_actions_then_key()
    {
        var few = Page("a", "Schedule week", "surface:screen/A", "suggested", Action("one", "One"));
        var many = Page("b", "Schedule week", "surface:screen/B", "suggested", Action("one", "One"), Action("two", "Two"));
        var sameAsMany = Page("c", "Schedule week", "surface:screen/C", "suggested", Action("one", "One"), Action("two", "Two"));

        Assert.Equal("surface:screen/B", Preview("Schedule week", few, many, sameAsMany)[0].Match?.Key.Value);
        Assert.Equal("surface:screen/B", Preview("Schedule week", sameAsMany, few, many)[0].Match?.Key.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_naming_no_page_is_new_and_gets_its_slug()
    {
        var rows = Preview("Billing\nSend invoice", ScheduleWeek());

        Assert.Null(rows[0].Match);
        Assert.Null(rows[0].Replaces);
        Assert.True(rows[0].Ticked);
        Assert.Equal("billing", rows[0].Page?.Id.Value);
        Assert.Null(rows[0].Page?.SurfaceKey);
        Assert.Null(rows[0].Page?.Actions[0].ActionKey);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_page_whose_id_is_an_owner_pages_replaces_it_and_owner_pages_never_match()
    {
        var owner = Page("billing", "Billing", "surface:screen/Billing", "owner");

        var rows = Preview("Billing", owner);

        Assert.Null(rows[0].Match);
        Assert.Equal("billing", rows[0].Replaces?.Value);
        Assert.True(rows[0].Ticked);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_new_blocks_with_the_same_id_are_the_same_page()
    {
        var rows = Preview("Billing\n\nbilling");

        Assert.Equal(1, rows[1].SameAs);
        Assert.Equal("Not added: Same page as block 1", rows[1].Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_matched_action_with_no_concepts_written_keeps_the_ones_the_map_suggests()
    {
        var row = Preview("Schedule week\nSubmit schedule\nCancel schedule -> Approval workflow", ScheduleWeek())[0];

        Assert.Equal("action:WeekPage.submit", row.Actions[0].Match?.Key.Value);
        Assert.Equal("self-scheduling", string.Join('|', row.Actions[0].Concepts.Select(static c => c.Id?.Value)));
        Assert.Equal(["self-scheduling"], row.Page?.Actions[0].Concepts);
        Assert.Equal("action:WeekPage.submit", row.Page?.Actions[0].ActionKey);
        Assert.Equal("approval-workflow", string.Join('|', row.Actions[1].Concepts.Select(static c => c.Id?.Value)));
        Assert.Equal(Week, row.Page?.SurfaceKey);
        Assert.Equal("aw-split-panes", row.Page?.Repo?.Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_action_the_page_lacks_stays_unmatched()
    {
        var row = Preview("Schedule week\nPrint roster", ScheduleWeek())[0];

        Assert.Null(row.Actions[0].Match);
        Assert.Null(row.Page?.Actions[0].ActionKey);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_concept_resolves_by_id_with_or_without_the_prefix_or_by_name_pins_first()
    {
        (ConceptId Id, string Name)[] concepts =
        [
            (ConceptId.From("invoices"), "Billing"), (ConceptId.From("billing-map"), "Billing"), (ConceptId.From("approval-workflow"), "Approval workflow"),
        ];

        var rows = PageListPreview.Of(PageListText.Parse("Home\nPay -> concept:approval-workflow, approval-workflow, billing, APPROVAL WORKFLOW"), [], concepts);

        Assert.Equal("approval-workflow|approval-workflow|invoices|approval-workflow", string.Join('|', rows[0].Actions[0].Concepts.Select(static c => c.Id?.Value)));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_concept_unticks_the_row_and_is_listed_once()
    {
        var row = Preview("Home\nPay -> aproval, Self-scheduling, aproval")[0];

        Assert.Equal(["aproval"], row.UnknownConcepts);
        Assert.False(row.Ticked);
        Assert.Equal("No concept called \"aproval\": pick one", row.Reason);
        Assert.Null(row.Page);
        Assert.Null(row.Actions[0].Concepts[0].Id);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_page_name_over_80_characters_is_not_added()
    {
        var row = Preview(new string('a', 81))[0];

        Assert.False(row.Ticked);
        Assert.Equal("Not added: the name is 81 characters; at most 80", row.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_action_name_over_80_characters_names_the_action()
    {
        var row = Preview("Home\nOpen\n" + new string('b', 81))[0];

        Assert.Equal("Not added: action 2's name is 81 characters; at most 80", row.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void More_than_40_actions_is_not_added()
    {
        var row = Preview("Home\n" + string.Join('\n', Enumerable.Range(1, 41).Select(static i => "Action " + i.ToString(CultureInfo.InvariantCulture))))[0];

        Assert.Equal("Not added: 41 actions; a page holds at most 40", row.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_action_with_more_than_20_concepts_is_not_added()
    {
        var ids = Enumerable.Range(1, 21).Select(static i => "c" + i.ToString(CultureInfo.InvariantCulture)).ToList();
        var concepts = ids.Select(static id => (ConceptId.From(id), id)).ToList();

        var row = PageListPreview.Of(PageListText.Parse("Home\nOpen\nPay -> " + string.Join(", ", ids)), [], concepts)[0];

        Assert.Equal("Not added: action 2 relates to 21 concepts; at most 20", row.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_actions_with_the_same_name_are_not_added()
    {
        var row = Preview("Home\nSubmit\nCancel\nsubmit")[0];

        Assert.Equal("Not added: two actions are called submit", row.Reason);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_problem_wins_in_the_order_same_page_then_rules_then_concepts()
    {
        var rows = Preview(
            "Schedule week\n\nSchedule week\nPay -> aproval\n\n" + new string('x', 81) + "\nPay -> aproval\n\nHome\nPay -> aproval", ScheduleWeek());

        Assert.Equal("Not added: Same page as block 1", rows[1].Reason);
        Assert.Equal("Not added: the name is 81 characters; at most 80", rows[2].Reason);
        Assert.Equal("No concept called \"aproval\": pick one", rows[3].Reason);
    }
}
