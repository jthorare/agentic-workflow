using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1701: each action row of the page sheet has its own "Pick another" over the map's suggested actions on the page's map surface (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class PageSheetActionPickTests
{
    private const string WeekKey = "surface:route/GET:/schedule/week";
    private const string SubmitKey = "action:button/Submit week";
    private const string ApproveKey = "action:button/Approve week";
    private const string CopyKey = "action:button/Copy week";
    private const string ExportKey = "action:button/Export week";

    private static readonly PageMatch _copy = new(CopyKey, "Copy week");

    private static async Task<(PageSheetViewModel Sheet, FakePageWrites Writes, FakeTimeProvider Clock)> Opened(bool withSurface = true)
    {
        var writes = new FakePageWrites
        {
            ActionMatches = [new PageMatch(SubmitKey, "Submit week"), new PageMatch(ApproveKey, "Approve week"), _copy, new PageMatch(ExportKey, "Export week")],
        };
        writes.Add(WeekKey, new PageDraft("schedule-week", "Schedule week", "web", withSurface ? WeekKey : null, Owner: true,
        [
            new PageActionDraft("submit", "Submit week", SubmitKey, ["approval"]),
            new PageActionDraft("approve", "Approve week", ApproveKey, ["approval"]),
            new PageActionDraft("print", "Print week", Action: null, ["approval"]),
        ]));
        var clock = new FakeTimeProvider();
        var sheet = new PageSheetViewModel(writes, [], [], openConcept: null, clock);
        await sheet.OpenPage(WeekKey, TestContext.Current.CancellationToken);
        return (sheet, writes, clock);
    }

    private static async Task Type(PageSheetViewModel sheet, PageSheetActionViewModel row, FakeTimeProvider clock, string text)
    {
        row.PickQuery = text;
        clock.Advance(PageSheetViewModel.PickDebounce);
        await sheet.ActionPicking;
    }

    [Fact]
    public async Task A_rows_pick_another_reads_the_surfaces_map_actions_and_lists_them_by_name()
    {
        var (sheet, writes, _) = await Opened();
        var print = sheet.Actions[2];

        sheet.OpenActionPick(print);
        await sheet.ActionPicking;

        Expect.Equal("Pick another", print.PickAnotherText);
        Expect.True(print.IsPicking, "the search opens under the row");
        Expect.Sequence([WeekKey], writes.ActionReads);
        Expect.Sequence(["Copy week", "Export week"], print.PickRows.Select(r => r.Name));   // the two other rows' actions are not offered
    }

    [Fact]
    public async Task An_action_another_row_holds_and_the_rows_own_are_left_out()
    {
        var (sheet, _, _) = await Opened();
        var submit = sheet.Actions[0];

        sheet.OpenActionPick(submit);
        await sheet.ActionPicking;

        Expect.Sequence(["Copy week", "Export week"], submit.PickRows.Select(r => r.Name));   // not its own Submit week, not Approve week of the second row
    }

    [Fact]
    public async Task An_action_a_row_was_repointed_to_in_this_sheet_is_left_out_of_the_others()
    {
        var (sheet, _, _) = await Opened();
        sheet.OpenActionPick(sheet.Actions[2]);
        await sheet.ActionPicking;
        sheet.ChooseAction(_copy);

        sheet.OpenActionPick(sheet.Actions[0]);
        await sheet.ActionPicking;

        Expect.Sequence(["Export week"], sheet.Actions[0].PickRows.Select(r => r.Name));
    }

    [Fact]
    public async Task Typing_filters_by_name_without_regard_to_case()
    {
        var (sheet, _, clock) = await Opened();
        var print = sheet.Actions[2];
        sheet.OpenActionPick(print);
        await sheet.ActionPicking;

        await Type(sheet, print, clock, "EXP");

        Expect.Sequence(["Export week"], print.PickRows.Select(r => r.Name));
        Expect.Equal(string.Empty, print.PickStatus);
    }

    [Fact]
    public async Task No_match_says_so_with_the_query()
    {
        var (sheet, _, clock) = await Opened();
        var print = sheet.Actions[2];
        sheet.OpenActionPick(print);
        await sheet.ActionPicking;

        await Type(sheet, print, clock, "x");

        Expect.Equal("No action matches \"x\"", print.PickStatus);
        Expect.Sequence([], print.PickRows.Select(r => r.Name));
    }

    [Fact]
    public async Task A_blank_search_with_nothing_left_says_the_page_has_no_other_action()
    {
        var (sheet, writes, _) = await Opened();
        writes.ActionMatches = [];
        var print = sheet.Actions[2];

        sheet.OpenActionPick(print);
        await sheet.ActionPicking;

        Expect.Equal("No other actions on this page yet.", print.PickStatus);
        Expect.Equal("Search this page's actions", print.PickBoxName);
    }

    [Fact]
    public async Task Searching_shows_while_the_read_runs()
    {
        var (sheet, writes, _) = await Opened();
        writes.ActionsGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var print = sheet.Actions[2];

        sheet.OpenActionPick(print);
        await Task.Yield();

        Expect.Equal("Searching…", print.PickStatus);
        writes.ActionsGate.SetResult();
        await sheet.ActionPicking;
        Expect.Equal(string.Empty, print.PickStatus);
    }

    [Fact]
    public async Task A_failed_read_shows_the_error_line()
    {
        var (sheet, writes, _) = await Opened();
        writes.ActionsFail = true;
        var print = sheet.Actions[2];

        sheet.OpenActionPick(print);
        await sheet.ActionPicking;

        Expect.Equal("Couldn't read the page's actions. Try again.", print.PickStatus);
        Expect.True(print.HasPickStatus, "the error line shows");
    }

    [Fact]
    public async Task Picking_repoints_that_row_only_and_closes_its_search()
    {
        var (sheet, _, _) = await Opened();
        var print = sheet.Actions[2];
        sheet.OpenActionPick(print);
        await sheet.ActionPicking;
        Expect.True(!sheet.HasChanges, "nothing changed yet");

        sheet.ChooseAction(_copy);

        Expect.True(!print.IsPicking, "the search closes");
        Expect.Equal(CopyKey, print.Action);
        Expect.Equal("From the map: Copy week", print.FromMap);
        Expect.Equal(SubmitKey, sheet.Actions[0].Action);
        Expect.Equal(ApproveKey, sheet.Actions[1].Action);
        Expect.True(sheet.HasChanges, "a repointed row asks before a discard");
    }

    [Fact]
    public async Task Saving_after_a_pick_writes_the_picked_action_for_that_row()
    {
        var (sheet, writes, _) = await Opened();
        sheet.OpenActionPick(sheet.Actions[2]);
        await sheet.ActionPicking;
        sheet.ChooseAction(_copy);

        await sheet.Save(TestContext.Current.CancellationToken);

        var actions = writes.Saves[0].Save.Set[0].Actions;
        Expect.Sequence([SubmitKey, ApproveKey, CopyKey], actions.Select(a => a.Action));
        Expect.Equal("print", actions[2].Id);
    }

    [Fact]
    public async Task One_search_is_open_at_a_time_across_the_page_and_the_rows()
    {
        var (sheet, _, _) = await Opened();
        var (submit, print) = (sheet.Actions[0], sheet.Actions[2]);

        sheet.OpenPick();
        sheet.OpenActionPick(print);
        Expect.True(!sheet.IsPicking, "a row search closes the page search");
        Expect.True(print.IsPicking, "the row search is open");

        sheet.OpenActionPick(submit);
        Expect.True(!print.IsPicking, "another row's search closes the first");
        Expect.True(submit.IsPicking, "the second row's search is open");

        sheet.OpenPick();
        Expect.True(!submit.IsPicking, "the page search closes a row search");
        Expect.True(sheet.IsPicking, "the page search is open");
    }

    [Fact]
    public async Task Esc_closes_the_open_search_before_the_sheet()
    {
        var (sheet, _, _) = await Opened();
        var closed = 0;
        sheet.Closed += (_, _) => closed++;
        var print = sheet.Actions[2];
        sheet.OpenActionPick(print);
        await sheet.ActionPicking;

        Expect.True(sheet.Escape(), "Esc is handled");
        Expect.True(!print.IsPicking, "the search closed");
        Expect.Equal(0, closed);

        _ = sheet.Escape();
        Expect.Equal(1, closed);
    }

    [Fact]
    public async Task A_page_with_no_map_surface_shows_no_row_pick_another()
    {
        var (sheet, _, _) = await Opened(withSurface: false);

        Expect.True(sheet.Actions.All(a => !a.CanPick), "no row offers Pick another");
        sheet.OpenActionPick(sheet.Actions[2]);
        Expect.True(!sheet.Actions[2].IsPicking, "and none opens");
    }

    [Fact]
    public async Task A_page_with_a_map_surface_shows_pick_another_on_every_row_even_one_with_no_code_yet()
    {
        var (sheet, _, _) = await Opened();

        Expect.True(sheet.Actions.All(a => a.CanPick), "every row offers Pick another");
        sheet.AddAction();
        Expect.True(sheet.Actions[^1].CanPick, "a new row offers it too");
        Expect.Equal("No code found yet", sheet.Actions[^1].FromMap);
    }
}
