using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
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

    // The no-map refusal as serve sends it for a profile of several repositories: one build maps them all.
    private static RouteAnswer NoMapFor(params string[] repos) => new(HttpStatusCode.NotFound,
        "{\"type\":\"about:blank\",\"title\":\"SND-CONTEXT-030\",\"status\":404,\"detail\":\"there is no map yet\",\"noMap\":{\"repos\":["
        + string.Join(',', repos.Select(r => "\"" + r + "\"")) + "]},\"traceId\":\"<trace>\"}");

    [Fact]
    public async Task No_map_for_two_repositories_says_the_one_build_maps_both_by_name()
    {
        var page = await Page(new FakeSindriApi { WholeMapAnswer = _ => NoMapFor("aw-split-panes", "tabsrepo") });

        Assert.Equal(
            ("No map yet for 2 repositories", "Build map makes one map of both, aw-split-panes and tabsrepo: what they are made of and how their parts connect."),
            (page.EmptyTitle, page.EmptyBody));
        Assert.NotNull(page.BuildControl);
    }

    [Fact]
    public async Task No_map_for_three_repositories_counts_them_in_the_sentence()
    {
        var page = await Page(new FakeSindriApi { WholeMapAnswer = _ => NoMapFor("a", "b", "c") });

        Assert.Equal("Build map makes one map of all 3 repositories: what they are made of and how their parts connect.", page.EmptyBody);
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

    [Fact]
    public async Task A_refused_area_read_with_no_map_offers_one_build_button_that_starts_the_build()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Area is null
                ? RouteAnswer.Api("context-whole", "areas-over-budget")
                : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "there is no map yet (fix: sindri context map import <file>)"),
        };
        var page = await Page(api);

        await page.OpenWholeArea("feature:nope", Token);

        Assert.Equal(("There is no map yet.", "BuildContextMap"), (page.Problem, page.ProblemFix?.Action));
        await page.ProblemFix!.Press(Token);
        Assert.Equal(1, api.BuildContextMapCalls);
    }

    [Fact]
    public async Task A_refusal_that_the_app_cannot_fix_has_no_button()
    {
        var api = new FakeSindriApi
        {
            WholeMapAnswer = q => q.Area is null
                ? RouteAnswer.Api("context-whole", "areas-over-budget")
                : RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-200", "feature:nope isn't in this map"),
        };
        var page = await Page(api);

        await page.OpenWholeArea("feature:nope", Token);

        Assert.Null(page.ProblemFix);
    }
}
