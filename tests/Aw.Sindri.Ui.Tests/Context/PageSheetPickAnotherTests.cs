using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>#1701 P2b: "Pick another" in the page sheet searches the map's pages and repoints the page's map surface (pages spec §3.6).</summary>
[Lane(TestLane.Unit)]
public sealed class PageSheetPickAnotherTests
{
    private const string WeekKey = "surface:screen/week";
    private const string DayKey = "surface:screen/day";

    private static readonly PageMatch _day = new(DayKey, "Schedule day");

    private static async Task<(PageSheetViewModel Sheet, FakePageWrites Writes, FakeTimeProvider Clock)> Opened()
    {
        var writes = new FakePageWrites { Matches = [new PageMatch(WeekKey, "Schedule week"), _day] };
        writes.Add(WeekKey, new PageDraft("schedule-week", "Schedule week", "web", WeekKey, Owner: false, []));
        var clock = new FakeTimeProvider();
        var sheet = new PageSheetViewModel(writes, [], [], openConcept: null, clock);
        await sheet.OpenPage(WeekKey, TestContext.Current.CancellationToken);
        return (sheet, writes, clock);
    }

    private static async Task Type(PageSheetViewModel sheet, FakeTimeProvider clock, string text)
    {
        sheet.PickQuery = text;
        clock.Advance(PageSheetViewModel.PickDebounce);
        await sheet.Picking;
    }

    [Fact]
    public async Task Pick_another_searches_the_maps_pages_by_name()
    {
        var (sheet, writes, clock) = await Opened();

        sheet.OpenPick();
        await Type(sheet, clock, "schedul");

        Expect.Equal("Pick another", sheet.PickAnotherText);
        Expect.True(sheet.IsPicking, "the search opens");
        Expect.Equal(("web", "schedul", 50), writes.Searches[^1]);
        Expect.Sequence(["Schedule day"], sheet.PickRows.Select(r => r.Name));   // the page's own surface is not offered
    }

    [Fact]
    public async Task Searching_shows_while_the_read_runs()
    {
        var (sheet, writes, clock) = await Opened();
        writes.SearchGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        sheet.OpenPick();

        sheet.PickQuery = "day";
        clock.Advance(PageSheetViewModel.PickDebounce);
        await Task.Yield();

        Expect.Equal("Searching…", sheet.PickStatus);
        writes.SearchGate.SetResult();
        await sheet.Picking;
        Expect.Equal(string.Empty, sheet.PickStatus);
    }

    [Fact]
    public async Task No_match_says_so_with_the_query()
    {
        var (sheet, writes, clock) = await Opened();
        writes.Matches = [];
        sheet.OpenPick();

        await Type(sheet, clock, "schedul");

        Expect.Equal("No page matches \"schedul\"", sheet.PickStatus);
        Expect.Sequence([], sheet.PickRows.Select(r => r.Name));
    }

    [Fact]
    public async Task A_blank_search_that_finds_nothing_says_the_map_has_no_other_page()
    {
        var (sheet, writes, clock) = await Opened();
        writes.Matches = [];
        sheet.OpenPick();

        await Type(sheet, clock, "   ");

        Expect.Equal("No other pages on the map yet.", sheet.PickStatus);
    }

    [Fact]
    public async Task A_failed_read_shows_the_error_line()
    {
        var (sheet, writes, clock) = await Opened();
        writes.SearchFails = true;
        sheet.OpenPick();

        await Type(sheet, clock, "schedul");

        Expect.Equal("Couldn't read the map's pages. Try again.", sheet.PickStatus);
        Expect.True(sheet.HasPickStatus, "the error line shows");
    }

    [Fact]
    public async Task Picking_a_row_repoints_the_page_and_its_from_the_map_line()
    {
        var (sheet, _, clock) = await Opened();
        sheet.OpenPick();
        await Type(sheet, clock, "day");

        sheet.Choose(_day);

        Expect.True(!sheet.IsPicking, "the search closes");
        Expect.Equal("From the map: day", sheet.FromMap);
        Expect.True(sheet.HasChanges, "a repointed page asks before a discard");
    }

    [Fact]
    public async Task Saving_after_a_pick_writes_the_new_surface()
    {
        var (sheet, writes, clock) = await Opened();
        sheet.OpenPick();
        await Type(sheet, clock, "day");
        sheet.Choose(_day);

        await sheet.Save(TestContext.Current.CancellationToken);

        Expect.Equal(DayKey, writes.Saves[0].Save.Set[0].Surface);
    }

    [Fact]
    public void The_pages_route_carries_q()
    {
        var route = ContextRoutes.Pages(new PagesQuery(Repo: "web", Limit: 50, Marks: "all") { Q = "sched ule" });

        Expect.True(route.Contains("q=sched%20ule", StringComparison.Ordinal), route);
    }

    [Fact]
    public void The_pages_route_carries_page_escaped()
    {
        var route = ContextRoutes.Pages(new PagesQuery { Page = "surface:route/GET:/schedule/week" });

        Expect.True(route.Contains("page=surface%3Aroute%2FGET%3A%2Fschedule%2Fweek", StringComparison.Ordinal), route);
    }
}
