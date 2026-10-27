using System.Globalization;
using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>UX pass C-1: the whole map follows the stored map. A newer map version reaches the page on the next event and when the page is shown again, with no restart.</summary>
[Lane(TestLane.Unit)]
public sealed class ContextWholeFollowTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ContextPageViewModel Page, List<Func<Uri, CancellationToken, Task>> Follows, Uri Url)> Page(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var follows = new List<Func<Uri, CancellationToken, Task>>();
        return (new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, follows.Add)), follows, url);
    }

    private static RouteAnswer StatusAt(int number) =>
        new(HttpStatusCode.OK, GoldenFiles.Api("context-map", "map-tabs-anchor").Replace("\"number\": 1", $"\"number\": {number.ToString(CultureInfo.InvariantCulture)}", StringComparison.Ordinal));

    [Fact]
    public async Task A_newer_map_version_is_read_on_the_next_event()
    {
        var api = new FakeSindriApi();
        var (page, follows, url) = await Page(api);
        await page.Opened(Token);
        _ = Assert.Single(api.WholeMapAsked);

        api.MapStatusAnswer = StatusAt(2);
        foreach (var follow in follows)
        {
            await follow(url, Token);
        }

        Assert.Equal(2, api.WholeMapAsked.Count);
    }

    [Fact]
    public async Task The_same_map_version_is_not_read_again_on_an_event()
    {
        var api = new FakeSindriApi();
        var (page, follows, url) = await Page(api);
        await page.Opened(Token);

        foreach (var follow in follows)
        {
            await follow(url, Token);
        }

        _ = Assert.Single(api.WholeMapAsked);
    }

    [Fact]
    public async Task Showing_the_page_again_reads_a_newer_map_version()
    {
        var api = new FakeSindriApi();
        var (page, _, _) = await Page(api);
        await page.Opened(Token);

        api.MapStatusAnswer = StatusAt(2);
        await page.Opened(Token);

        Assert.Equal(2, api.WholeMapAsked.Count);
    }
}
