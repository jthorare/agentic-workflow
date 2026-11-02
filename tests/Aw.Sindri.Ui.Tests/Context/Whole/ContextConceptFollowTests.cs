using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Context.Concepts;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#1930: a concept's open map follows the stored map like the whole map does. A newer map version is read on the next event, for the same concept, and a drilled band stays drilled.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextConceptFollowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ContextPageViewModel Page, FakeSindriApi Api, List<Func<Uri, CancellationToken, Task>> Follows, Uri Url)> OpenBilling()
    {
        var api = ConceptPage.Serving(AConcepts.Three());
        api.WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query) : RouteAnswer.Api("context-whole", "concept-open");
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, follows.Add));
        await page.Opened(Token);
        await page.OpenConcept("concept:billing", Token);
        return (page, api, follows, url);
    }

    private static RouteAnswer StatusAt(int number) =>
        new(HttpStatusCode.OK, GoldenFiles.Api("context-map", "map-tabs-anchor").Replace("\"number\": 1", $"\"number\": {number.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal));

    private static async Task Event(List<Func<Uri, CancellationToken, Task>> follows, Uri url)
    {
        foreach (var follow in follows)
        {
            await follow(url, Token);
        }
    }

    [Fact]
    public async Task An_open_concept_reads_a_newer_map_version_on_the_next_event()
    {
        var (_, api, follows, url) = await OpenBilling();
        var before = api.WholeMapAsked.Count;

        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);

        Expect.Equal(before + 1, api.WholeMapAsked.Count);
        Expect.Equal("concept:billing", api.WholeMapAsked[^1].Concept);
    }

    [Fact]
    public async Task An_open_concept_draws_the_newer_version_in_its_header()
    {
        var (page, api, follows, url) = await OpenBilling();
        Expect.Equal("Context · the whole map · v1", page.Whole.Head);
        api.WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query)
            : new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("context-whole", "concept-open").Replace("\"mapVersion\": \"v1\"", "\"mapVersion\": \"v2\"", StringComparison.Ordinal));

        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);

        Expect.Equal("Context · the whole map · v2", page.Whole.Head);
    }

    [Fact]
    public async Task An_open_concept_is_not_read_again_for_the_same_map_version()
    {
        var (_, api, follows, url) = await OpenBilling();
        var before = api.WholeMapAsked.Count;

        await Event(follows, url);

        Expect.Equal(before, api.WholeMapAsked.Count);
    }

    [Fact]
    public async Task A_drilled_band_stays_drilled_when_the_newer_version_is_drawn()
    {
        var (page, api, follows, url) = await OpenBilling();
        page.Whole.DrillInto(page.Whole.BandButtons[0].Band);   // the Why drill's way in: a band of the concept's map
        var before = api.WholeMapAsked.Count;

        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);

        Expect.Equal((before + 1, true), (api.WholeMapAsked.Count, page.Whole.IsDrilled));
    }

    [Fact]
    public async Task An_open_concept_gone_from_the_newer_version_goes_back_to_the_concepts_with_a_note()
    {
        var (page, api, follows, url) = await OpenBilling();
        api.WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query) : new RouteAnswer(HttpStatusCode.NotFound, GoldenFiles.Api("context-whole", "concept-unknown-200"));

        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);

        Expect.Equal((null, true, "Billing isn't in map v2 any more", false), (page.OpenedConceptId, page.ShowConcepts, page.Concepts.Note, page.Whole.IsFailed));
    }

    [Fact]
    public async Task An_open_concept_whose_newer_read_fails_otherwise_shows_the_failure_row()
    {
        var (page, api, follows, url) = await OpenBilling();
        api.WholeMapAnswer = query => query.Concept is null ? ContextAnswers.WholeMap(query) : new RouteAnswer(HttpStatusCode.InternalServerError, "{}");

        api.MapStatusAnswer = StatusAt(2);
        await Event(follows, url);

        Expect.Equal(("concept:billing", true, true, false), (page.OpenedConceptId, page.Whole.IsFailed, page.ShowWholeStatus, page.Concepts.HasNote));
    }
}
