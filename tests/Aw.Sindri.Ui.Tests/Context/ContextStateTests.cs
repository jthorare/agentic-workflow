using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Context page's states (F3 spec 1, slice 4): which overlay shows when, the build action, and the refusal banner.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Anchor => (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;

    private static RouteAnswer NoMap => RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-CONTEXT-030", "no map yet (fix: sindri context map build)");

    private static async Task<(ContextPageViewModel Page, List<Func<Uri, CancellationToken, Task>> Follows)> Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        return (new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, follows.Add)), follows);
    }

    [Theory]
    [InlineData(false, false, "None")]
    [InlineData(true, false, "Empty")]
    [InlineData(true, true, "Building")]
    [InlineData(false, true, "Building")]
    public void The_overlay_is_building_when_a_build_runs_else_empty_when_there_is_no_map_else_none(bool noMap, bool building, string expected) =>
        Assert.Equal(expected, ContextStates.Overlay(noMap, building).ToString());

    [Fact]
    public async Task A_read_with_a_map_shows_no_overlay_and_no_banner()
    {
        var (page, _) = await Page(new FakeSindriApi());
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((ContextOverlay.None, false, false, true), (page.Overlay, page.ShowEmpty, page.HasProblem, page.MapVisible));
    }

    [Fact]
    public async Task The_served_no_map_answer_shows_the_empty_state_with_a_build_button_and_no_banner()
    {
        var (page, _) = await Page(new FakeSindriApi { LensesAnswer = _ => NoMap });
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal((ContextOverlay.Empty, true, false, false, false), (page.Overlay, page.ShowEmpty, page.ShowBuilding, page.HasProblem, page.MapVisible));
        Assert.Equal((Labels.ContextStateEmptyTitle, Labels.ContextStateEmptyBody), (page.EmptyTitle, page.EmptyBody));
        Assert.Equal("BuildContextMap", page.BuildControl?.Action);
        Assert.True(page.BuildControl?.CanRun);
    }

    [Fact]
    public async Task Any_other_refusal_is_a_banner_with_no_overlay_and_the_canvas_stays_hidden()
    {
        var (page, _) = await Page(new FakeSindriApi());
        page.AnchorText = "feature:nobody.knows";

        await page.Submit(Token);

        Assert.Equal((ContextOverlay.None, true, false), (page.Overlay, page.HasProblem, page.HasStack));
        Assert.False(page.MapVisible);
        Assert.NotEmpty(page.Problem);
    }

    [Fact]
    public async Task Pressing_build_starts_the_job_once_and_shows_the_building_state()
    {
        var server = new FakeSindriApi { LensesAnswer = _ => NoMap };
        var (page, _) = await Page(server);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        await page.BuildControl!.Press(Token);

        Assert.Equal(1, server.BuildContextMapCalls);
        Assert.Equal((ContextOverlay.Building, true, false), (page.Overlay, page.ShowBuilding, page.ShowEmpty));
        Assert.Equal((Labels.ContextStateBuilding, Labels.ContextStateBuildingBody), (page.BuildingTitle, page.BuildingBody));
    }

    [Fact]
    public async Task A_build_already_running_counts_as_building_not_as_a_failure()
    {
        var server = new FakeSindriApi { LensesAnswer = _ => NoMap, BuildContextMapAnswer = RouteAnswer.Problem("context-map-refresh", "refused-running") };
        var (page, _) = await Page(server);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        await page.BuildControl!.Press(Token);

        Assert.Equal(ContextOverlay.Building, page.Overlay);
    }

    [Fact]
    public async Task A_refused_build_leaves_the_empty_state_with_the_refusal_on_the_button()
    {
        var server = new FakeSindriApi { LensesAnswer = _ => NoMap, BuildContextMapAnswer = RouteAnswer.Problem(HttpStatusCode.Forbidden, "SND-AUTH-002", "An agent session can't do this.") };
        var (page, _) = await Page(server);
        page.AnchorText = Anchor;
        await page.Submit(Token);

        await page.BuildControl!.Press(Token);

        Assert.Equal((ContextOverlay.Empty, "SND-AUTH-002"), (page.Overlay, page.BuildControl!.ErrorCode));
    }

    [Fact]
    public async Task A_build_job_already_running_when_the_no_map_answer_comes_shows_building_at_once()
    {
        var server = new FakeSindriApi
        {
            LensesAnswer = _ => NoMap,
            JobsAnswer = new RouteAnswer(HttpStatusCode.OK, GoldenFiles.Api("jobs", "running").Replace("EchoJob", "BuildContextMap", StringComparison.Ordinal)),
        };
        var (page, _) = await Page(server);
        page.AnchorText = Anchor;

        await page.Submit(Token);

        Assert.Equal(ContextOverlay.Building, page.Overlay);
    }

    [Fact]
    public async Task When_the_build_job_ends_the_map_is_read_again_and_the_overlay_goes()
    {
        var server = new FakeSindriApi { LensesAnswer = _ => NoMap };
        var (page, follows) = await Page(server);
        page.AnchorText = Anchor;
        await page.Submit(Token);
        await page.BuildControl!.Press(Token);
        server.LensesAnswer = ContextAnswers.Lenses;   // the build wrote the map
        server.JobAnswers["JOB-000000000000000000000000000000a1"] = RouteAnswer.Api("jobs-id", "succeeded");   // the job the start golden names

        foreach (var follow in follows)
        {
            await follow(new Uri("http://127.0.0.1:1/"), Token);
        }

        Assert.Equal((ContextOverlay.None, true), (page.Overlay, page.MapVisible));
    }
}
