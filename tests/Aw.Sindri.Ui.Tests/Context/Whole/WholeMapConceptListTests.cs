using System.Globalization;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A concept opens its own member list (#3674): tabs with the counts the card shows, a pager of 50, and a click on a code item the way an area item opens.</summary>
[Lane(TestLane.Unit)]
public sealed class WholeMapConceptListTests
{
    private static readonly TimeZoneInfo _utc = TimeZoneInfo.Utc;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A list page of one concept: 1980 code files, 71 features, 96 records (the card's 2147), 50 items a page.
    private static WholeMapView List(string tab = "all", int page = 1, int code = 1980, int features = 71, int records = 96)
    {
        var items = tab switch { "code" => code, "feature" => features, "record" => records, _ => code + features + records };
        var pages = Math.Max(1, (items + 49) / 50);
        var shown = Math.Min(50, Math.Max(0, items - ((page - 1) * 50)));
        var rows = Enumerable.Range(0, shown).Select(i => new WholeNode(string.Create(CultureInfo.InvariantCulture, $"code:src/F{i:0000}.cs"), string.Create(CultureInfo.InvariantCulture, $"F{i:0000}.cs"), "code", "area:unassigned", [], "trusted", "deterministic", 1, Confirmed: false)).ToList();
        return WholeMapLayoutTests.Draft("nodes-small") with { Nodes = rows, Edges = [], ConceptList = new WholeConceptList(tab, page, pages, items, code, features, records) };
    }

    private static WholeMapViewModel Concept(WholeMapView answer)
    {
        var map = new WholeMapViewModel(() => _utc);
        map.EnterConcept("Control plane", "concept:control");
        map.Show(answer);
        return map;
    }

    [Fact]
    public void The_tabs_carry_the_counts_the_card_shows_and_All_is_their_sum()
    {
        var map = Concept(List());

        Assert.Equal(["All 2147", "Code 1980", "Features 71", "Records 96"], map.ConceptTabs.Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(["all"], map.ConceptTabs.Where(t => t.IsSelected).Select(t => t.Key), StringComparer.Ordinal);
        Assert.True(map.IsConceptList);
        Assert.True(map.ShowAreaList);
        Assert.Null(map.Geometry);
    }

    [Fact]
    public void A_tab_asks_for_its_own_list_from_the_first_page_and_the_tab_on_screen_asks_for_nothing()
    {
        var map = Concept(List(page: 3));
        var asked = new List<(string Tab, int Page)>();
        map.ConceptListRequested += (_, e) => asked.Add(e.Value);

        map.ChooseTab("code");
        map.ChooseTab("all");

        Assert.Equal([("code", 1)], asked);
    }

    [Fact]
    public void A_tab_that_holds_nothing_shows_zero_and_is_not_selectable()
    {
        var map = Concept(List(records: 0));
        var asked = new List<(string Tab, int Page)>();
        map.ConceptListRequested += (_, e) => asked.Add(e.Value);

        map.ChooseTab("record");

        Assert.Equal(("Records 0", false), (map.ConceptTabs[3].Text, map.ConceptTabs[3].CanChoose));
        Assert.Empty(asked);
    }

    [Fact]
    public void The_pager_asks_for_the_next_and_previous_page_of_the_same_tab_and_stops_at_the_ends()
    {
        var map = Concept(List("code", page: 2));
        var asked = new List<(string Tab, int Page)>();
        map.ConceptListRequested += (_, e) => asked.Add(e.Value);

        map.NextPage();
        map.PreviousPage();
        map.Show(List("code", page: 40));
        map.NextPage();

        Assert.Equal([("code", 3), ("code", 1)], asked);
        Assert.Equal(("Page 40 of 40 · 30 of 1980 items", false), (map.AreaPageText, map.CanNextPage));
    }

    [Fact]
    public void A_concept_that_holds_nothing_says_so_and_has_no_pager()
    {
        var map = Concept(List(code: 0, features: 0, records: 0));

        Assert.True(map.ShowConceptEmpty);
        Assert.False(map.HasAreaPages);
        Assert.All(map.ConceptTabs, t => Assert.False(t.CanChoose));
    }

    [Fact]
    public void A_click_on_a_code_item_picks_that_node_the_way_an_area_item_does()
    {
        var map = Concept(List());
        var picked = new List<string>();
        map.NodeChosen += (_, e) => picked.Add(e.Value);

        map.ChooseItem(map.Items[0]);

        Assert.Equal(["code:src/F0000.cs"], picked);
    }

    [Fact]
    public void Leaving_the_concept_ends_the_list()
    {
        var map = Concept(List("code", page: 2));

        map.LeaveConcept();

        Assert.Equal((false, "all", 1), (map.IsConceptList, map.ConceptTab, map.ConceptPage));
    }

    private static async Task<(ContextPageViewModel Page, FakeSindriApi Api)> Open()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        api.WholeMapAnswer = query => query.Concept is null
            ? ContextAnswers.WholeMap(query)
            : new RouteAnswer(System.Net.HttpStatusCode.OK, JsonSerializer.Serialize(List(query.ConceptTab ?? "all", query.Page), SindriApiClient.Json));
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        await page.OpenConcept("concept:billing", Token);
        return (page, api);
    }

    [Fact]
    public async Task A_concept_click_asks_for_its_list_and_a_tab_switch_asks_with_the_tab()
    {
        var (page, api) = await Open();

        Assert.Equal(("concept:billing", "all", 1), (api.WholeMapAsked[^1].Concept, api.WholeMapAsked[^1].ConceptTab, api.WholeMapAsked[^1].Page));
        Assert.True(page.Whole.IsConceptList);

        await page.OpenConceptList("code", 1, Token);

        var asked = api.WholeMapAsked[^1];
        Assert.Equal(("concept:billing", "code", 1), (asked.Concept, asked.ConceptTab, asked.Page));
        Assert.Equal("code", page.Whole.ConceptTab);
    }

    [Fact]
    public async Task A_page_turn_asks_for_that_page_of_the_tab_on_screen()
    {
        var (page, api) = await Open();
        await page.OpenConceptList("code", 1, Token);

        page.Whole.NextPage();
        await Task.Yield();

        Assert.Equal(("code", 2), (api.WholeMapAsked[^1].ConceptTab, api.WholeMapAsked[^1].Page));
    }
}
