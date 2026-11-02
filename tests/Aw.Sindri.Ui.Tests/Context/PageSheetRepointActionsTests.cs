using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1701: repointing the page drops each row's map action, since those actions came from the old surface.</summary>
[Lane(TestLane.Unit)]
public sealed class PageSheetRepointActionsTests
{
    private const string WeekKey = "surface:screen/week";
    private const string AvailabilityKey = "surface:screen/availability";
    private const string SubmitKey = "action:button/Submit";

    private static async Task<(PageSheetViewModel Sheet, FakePageWrites Writes)> Opened()
    {
        var writes = new FakePageWrites { Matches = [new PageMatch(AvailabilityKey, "Availability")], ActionMatches = [new PageMatch("action:button/Copy", "Copy")] };
        writes.Add(WeekKey, new PageDraft("week", "Week", "web", WeekKey, Owner: true,
        [
            new PageActionDraft("submit", "Submit", SubmitKey, ["approval"]),
            new PageActionDraft("print", "Print", Action: null, ["approval"]),
        ]));
        var sheet = new PageSheetViewModel(writes, [], [], openConcept: null);
        await sheet.OpenPage(WeekKey, TestContext.Current.CancellationToken);
        return (sheet, writes);
    }

    [Fact]
    public async Task Repointing_the_page_clears_every_rows_map_action_and_keeps_names_and_ticks()
    {
        var (sheet, _) = await Opened();

        sheet.Choose(new PageMatch(AvailabilityKey, "Availability"));

        Expect.True(sheet.Actions.All(a => a.FromMap == Labels.ContextPagesNoCode), "every row reads No code found yet");
        Expect.Sequence(["Submit", "Print"], sheet.Actions.Select(a => a.Name));
        Expect.Sequence([["approval"], ["approval"]], sheet.Actions.Select(a => (IReadOnlyList<string>)[.. a.TickedIds]));
    }

    [Fact]
    public async Task Saving_after_a_repoint_writes_no_map_action()
    {
        var (sheet, writes) = await Opened();
        sheet.Choose(new PageMatch(AvailabilityKey, "Availability"));

        await sheet.Save(TestContext.Current.CancellationToken);

        var page = writes.Saves[0].Save.Set[0];
        Expect.Equal(AvailabilityKey, page.Surface);
        Expect.True(page.Actions.All(a => a.Action is null), "no row carries the old surface's action");
    }

    [Fact]
    public async Task Picking_the_same_surface_leaves_the_rows_alone()
    {
        var (sheet, _) = await Opened();

        sheet.Choose(new PageMatch(WeekKey, "Week"));

        Expect.Equal(SubmitKey, sheet.Actions[0].Action);
    }

    [Fact]
    public async Task Repointing_the_page_closes_an_open_row_search()
    {
        var (sheet, _) = await Opened();
        sheet.OpenActionPick(sheet.Actions[1]);
        await sheet.ActionPicking;
        Expect.True(sheet.Actions[1].IsPicking, "the row search is open");

        sheet.Choose(new PageMatch(AvailabilityKey, "Availability"));

        Expect.True(!sheet.Actions[1].IsPicking, "the row search closed");
        Expect.True(sheet.PickingRow is null, "no row search is open");
    }
}
