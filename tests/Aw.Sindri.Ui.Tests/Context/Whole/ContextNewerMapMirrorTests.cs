using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3261, the other way round and the failure path: a newer map version that has no concepts replaces the cards, and a newer version whose reads both fail leaves the map that was drawn.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextNewerMapMirrorTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static RouteAnswer StatusAt(int number) =>
        new(HttpStatusCode.OK, GoldenFiles.Api("context-map", "map-tabs-anchor").Replace("\"number\": 1", $"\"number\": {number.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal));

    private static async Task<(ContextPageViewModel Page, List<Func<Uri, CancellationToken, Task>> Follows, Uri Url)> Shown(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, follows.Add));
        await page.Opened(Token);
        return (page, follows, url);
    }

    private static async Task Event(List<Func<Uri, CancellationToken, Task>> follows, Uri url)
    {
        foreach (var follow in follows)
        {
            await follow(url, Token);
        }
    }

    [Fact]
    public async Task A_newer_map_without_concepts_replaces_the_cards_on_the_next_event()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var (page, follows, url) = await Shown(api);
        Expect.Equal((true, false), (page.ShowConcepts, page.ShowWhole));

        api.ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Without("empty"));
        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);

        Expect.Equal((false, true, Labels.ContextConceptsEmpty), (page.ShowConcepts, page.ShowWhole, page.Concepts.Note));
    }

    [Fact]
    public async Task A_newer_map_without_concepts_replaces_the_cards_when_the_page_is_opened_again()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var (page, _, _) = await Shown(api);

        api.ContextConceptsAnswer = _ => AConcepts.Served(AConcepts.Without("empty"));
        api.MapStatusAnswer = StatusAt(2);
        await page.Opened(Token);

        Expect.Equal((false, true, Labels.ContextConceptsEmpty), (page.ShowConcepts, page.ShowWhole, page.Concepts.Note));
    }

    [Fact]
    public async Task The_same_map_version_leaves_the_cards_alone_on_an_event()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var (page, follows, url) = await Shown(api);

        await Event(follows, url);

        Expect.Equal((true, 1), (page.ShowConcepts, api.ContextConceptsAsked.Count));
    }

    [Fact]
    public async Task A_newer_map_is_read_once_per_version_while_the_cards_stay()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        var (page, follows, url) = await Shown(api);

        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);
        await Event(follows, url);

        Expect.Equal((true, 2), (page.ShowConcepts, api.ContextConceptsAsked.Count));
    }

    [Fact]
    public async Task A_newer_map_whose_reads_both_fail_keeps_the_map_drawn_and_is_not_tried_again()
    {
        var failing = false;
        var api = new FakeSindriApi { ContextConceptsAnswer = _ => failing ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : AConcepts.Served(AConcepts.Without("empty")) };
        api.WholeMapAnswer = query => failing ? new RouteAnswer(HttpStatusCode.InternalServerError, "{}") : ContextAnswers.WholeMap(query);
        var (page, follows, url) = await Shown(api);
        Expect.Equal((true, 1, 1), (page.ShowWhole, api.WholeMapAsked.Count, api.ContextConceptsAsked.Count));

        failing = true;
        api.MapStatusAnswer = StatusAt(2);
        await page.Opened(Token);
        await Event(follows, url);
        await page.Opened(Token);

        Expect.Equal((true, false, 2, 2), (page.ShowWhole, page.Whole.IsFailed, api.WholeMapAsked.Count, api.ContextConceptsAsked.Count));
    }
}
