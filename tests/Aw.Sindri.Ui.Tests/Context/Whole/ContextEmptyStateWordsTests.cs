using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3761: the No map title names the repository, and no banner on the Context page quotes a command line.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextEmptyStateWordsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        return page;
    }

    [Fact]
    public async Task No_map_for_one_repository_names_it_instead_of_counting_it()
    {
        var page = await Page(new FakeSindriApi { WholeMapAnswer = _ => RouteAnswer.Problem("context-whole", "no-map-030") });

        Assert.Equal(("No map yet for aw-split-panes", "Build one to see what your repo is made of and how its parts connect."), (page.EmptyTitle, page.EmptyBody));
    }

    [Fact]
    public async Task A_refused_area_read_says_what_is_missing_without_the_command_line()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Area is null
                ? RouteAnswer.Api("context-whole", "areas-over-budget")
                : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-044", "there is no draft map (fix: sindri context map build)"),
        };
        var page = await Page(api);

        await page.OpenWholeArea("feature:nope", Token);

        Assert.Equal("There is no draft map yet.", page.Problem);
    }

    [Fact]
    public async Task A_refused_area_read_with_any_other_hint_keeps_the_sentence_and_drops_the_command()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Area is null
                ? RouteAnswer.Api("context-whole", "areas-over-budget")
                : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-999", "the map can't be read (fix: sindri doctor)"),
        };
        var page = await Page(api);

        await page.OpenWholeArea("feature:nope", Token);

        Assert.Equal("the map can't be read", page.Problem);
    }
}
