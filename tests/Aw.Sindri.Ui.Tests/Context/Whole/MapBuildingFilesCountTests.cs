using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#3452: serve sends the files count of the current repo while the whole map builds, and the building body line shows it once it is there.</summary>
[Lane(TestLane.Unit)]
public sealed class MapBuildingFilesCountTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 2, 0, 0, TimeSpan.Zero);   // why: the view model ages "startedAt" against the clock it is given, so the tests set it

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Page(string building)
    {
        var answer = new RouteAnswer(HttpStatusCode.NotFound,
            "{\"type\":\"about:blank\",\"title\":\"SND-CONTEXT-030\",\"status\":404,\"detail\":\"there is no map yet\",\"building\":" + building + ",\"traceId\":\"<trace>\"}");
        var api = new FakeSindriApi { WholeMapAnswer = _ => answer };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(_now);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }));
        await page.Opened(Token);
        return page;
    }

    [Fact]
    public async Task A_files_count_joins_the_body_between_the_sources_and_the_start()
    {
        var page = await Page("{\"sourcesDone\":1,\"sourcesTotal\":2,\"filesDone\":4,\"filesTotal\":7,\"startedAt\":\"2026-01-01T01:30:00.0000000Z\"}");

        Assert.Equal("1 of 2 sources · 4 of 7 files of the current repo · started 30 min ago", page.BuildingBody);
        Assert.Equal("Building the map · 1 of 2 sources · 4 of 7 files of the current repo · started 30 min ago", page.BuildingName);
    }

    [Fact]
    public async Task One_file_reads_in_the_singular()
    {
        var page = await Page("{\"filesDone\":0,\"filesTotal\":1}");

        Assert.Equal("0 of 1 file of the current repo", page.BuildingBody);
    }

    [Fact]
    public async Task Without_a_files_count_the_body_has_no_files_part()
    {
        var page = await Page("{\"sourcesDone\":1,\"sourcesTotal\":2,\"startedAt\":\"2026-01-01T01:30:00.0000000Z\"}");
        var half = await Page("{\"filesDone\":4}");

        Assert.Equal("1 of 2 sources · started 30 min ago", page.BuildingBody);
        Assert.DoesNotContain("files", half.BuildingBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Big_file_counts_group_their_digits()
    {
        var page = await Page("{\"filesDone\":150,\"filesTotal\":10224,\"startedAt\":\"2026-01-01T01:30:00.0000000Z\"}");

        Assert.Equal("150 of 10,224 files of the current repo · started 30 min ago", page.BuildingBody);
    }
}
