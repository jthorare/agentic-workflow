using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>
/// The whole map's not-ready overlays (whole-map spec §3.1) on the 404 SND-CONTEXT-030 context now fills: Building says how far the build is, when it
/// started, when the last map was synced and opens the job; No map says how many repositories wait, and a first build that has started shows Building.
/// Goldens from #866 (Golden/Api/context-whole: building-in-progress, no-map-030, no-map-approved, no-map-never-run).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class MapNotReadyTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 2, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // A 404 with the members as the API writes them (omitted, never null).
    private static RouteAnswer Refusal(string? building = null, string? noMap = null) => new(HttpStatusCode.NotFound,
        "{\"type\":\"about:blank\",\"title\":\"SND-CONTEXT-030\",\"status\":404,\"detail\":\"there is no map yet\""
        + (building is null ? string.Empty : ",\"building\":" + building) + (noMap is null ? string.Empty : ",\"noMap\":" + noMap) + ",\"traceId\":\"<trace>\"}");

    private static async Task<(ContextPageViewModel Page, List<ContextJobRequestedEventArgs> Jobs)> Page(RouteAnswer answer)
    {
        var api = new FakeSindriApi { WholeMapAnswer = _ => answer };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(_now);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        var jobs = new List<ContextJobRequestedEventArgs>();
        page.JobRequested += (_, e) => jobs.Add(e);
        await page.Opened(Token);
        return (page, jobs);
    }

    [Fact]
    public async Task A_building_answer_with_every_field_says_how_far_when_it_started_and_the_last_map()
    {
        var (page, _) = await Page(Refusal(building: """{"sourcesDone":3,"sourcesTotal":7,"startedAt":"2026-01-01T01:56:00.0000000Z","lastSyncAt":"2025-12-31T23:59:00.0000000Z","jobId":"JOB-1"}"""));

        Assert.Equal((true, false, false), (page.ShowBuilding, page.ShowEmpty, page.ShowWhole));
        Assert.Equal(("Building the map · 3 of 7 sources · started 4 min ago", "last map 2 h ago", true), (page.BuildingTitle, page.BuildingBody, page.HasBuildingBody));
        Assert.True(page.HasBuildJob);
    }

    [Fact]
    public async Task The_in_progress_golden_shows_Building_with_its_job()
    {
        var (page, _) = await Page(RouteAnswer.Problem("context-whole", "building-in-progress"));

        Assert.Equal((true, "Building the map · 1 of 1 source · started 2 h ago", "last map 2 h ago", true), (page.ShowBuilding, page.BuildingTitle, page.BuildingBody, page.HasBuildJob));
    }

    [Fact]
    public async Task A_building_answer_shows_only_the_parts_it_has()
    {
        var (counts, _) = await Page(Refusal(building: """{"sourcesDone":2,"sourcesTotal":5}"""));
        var (started, _) = await Page(Refusal(building: """{"startedAt":"2026-01-01T01:30:00.0000000Z"}"""));
        var (half, _) = await Page(Refusal(building: """{"sourcesDone":2}"""));
        var (bare, _) = await Page(Refusal(building: "{}"));

        Assert.Equal(("Building the map · 2 of 5 sources", string.Empty, false, false), (counts.BuildingTitle, counts.BuildingBody, counts.HasBuildingBody, counts.HasBuildJob));
        Assert.Equal(("Building the map · started 30 min ago", false), (started.BuildingTitle, started.HasBuildJob));
        Assert.Equal("Building the map", half.BuildingTitle);
        Assert.Equal((true, "Building the map", string.Empty), (bare.ShowBuilding, bare.BuildingTitle, bare.BuildingBody));
    }

    [Fact]
    public async Task No_map_names_how_many_repositories_wait_and_keeps_the_build_button()
    {
        var (two, _) = await Page(Refusal(noMap: """{"repos":["a","b"]}"""));
        var (one, _) = await Page(RouteAnswer.Problem("context-whole", "no-map-030"));

        Assert.Equal((true, false, "No map yet for 2 repositories"), (two.ShowEmpty, two.ShowBuilding, two.EmptyTitle));
        Assert.Equal((true, "No map yet for 1 repository"), (one.ShowEmpty, one.EmptyTitle));
        Assert.NotNull(two.BuildControl);
    }

    [Fact]
    public async Task An_old_answer_with_neither_member_keeps_the_existing_words()
    {
        var (page, _) = await Page(Refusal());

        Assert.Equal((true, Labels.ContextStateEmptyTitle), (page.ShowEmpty, page.EmptyTitle));
    }

    [Fact]
    public async Task A_first_build_that_has_started_shows_Building_not_No_map()
    {
        var (started, _) = await Page(RouteAnswer.Problem("context-whole", "no-map-approved"));
        var (never, _) = await Page(RouteAnswer.Problem("context-whole", "no-map-never-run"));

        Assert.Equal((true, false, "Building the map · started 2 h ago"), (started.ShowBuilding, started.ShowEmpty, started.BuildingTitle));
        Assert.Equal((false, true), (never.ShowBuilding, never.ShowEmpty));
    }

    [Fact]
    public async Task The_job_link_opens_that_job()
    {
        var (page, jobs) = await Page(RouteAnswer.Problem("context-whole", "building-in-progress"));
        var (none, noJobs) = await Page(Refusal(building: """{"sourcesDone":1}"""));

        page.RequestBuildJob();
        none.RequestBuildJob();

        Assert.Equal(new DeepLink(DeepLinkKind.Job, "JOB-00000000000000000000000000000011"), Assert.Single(jobs).Link);
        Assert.Empty(noJobs);
    }

    [Theory]
    [InlineData("building-in-progress")]
    [InlineData("no-map-030")]
    [InlineData("no-map-approved")]
    [InlineData("no-map-never-run")]
    public void The_two_members_read_and_write_back_exactly_what_the_golden_has(string scenario)
    {
        var served = GoldenFiles.Node("context-whole", scenario);
        var error = new Error("SND-CONTEXT-030", ErrorKind.NotFound, "x");
        foreach (var name in (string[])["building", "noMap"])
        {
            if (served[name] is { } member)
            {
                error = error.WithExtension(name, JsonDocument.Parse(member.ToJsonString()).RootElement.Clone());
            }
        }

        var read = MapNotReadyView.From(error, SindriApiClient.Json);
        var written = JsonNode.Parse(AwJson.Serialize(read, SindriApiClient.Json)) ?? throw new InvalidOperationException("no JSON");

        Assert.Equal(served["building"] is not null, read.Building is not null);
        Assert.Equal(served["noMap"] is not null, read.NoMap is not null);
        Assert.True(JsonNode.DeepEquals(Without(served), written), $"{scenario}: {written}");
    }

    private static JsonObject Without(JsonNode served)
    {
        var copy = new JsonObject();
        foreach (var name in (string[])["building", "noMap"])
        {
            if (served[name] is { } member)
            {
                copy[name] = member.DeepClone();
            }
        }

        return copy;
    }
}
