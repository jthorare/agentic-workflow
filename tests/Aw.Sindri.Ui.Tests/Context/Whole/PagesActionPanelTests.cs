using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>#1922: an action's name in Pages and actions opens its node panel and leaves the starting point alone (pages spec §3.4).</summary>
[Lane(TestLane.Unit)]
public sealed class PagesActionPanelTests
{
    private const string OwnerAction = "action:owner/week-schedule/submit-a-schedule";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<ContextPageViewModel> Open()
    {
        var api = new FakeSindriApi();
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
    public async Task An_actions_name_opens_its_node_panel_and_keeps_the_starting_point()
    {
        var page = await Open();
        page.AnchorText = "Tab manager";
        var before = page.AnchorText;

        page.Whole.OpenAction(OwnerAction);
        await page.Whole.Panel.Reading;

        Assert.True(page.Whole.Panel.IsOpen);
        Assert.Equal(OwnerAction, page.Whole.Panel.Key);
        Assert.Equal(before, page.AnchorText);
    }

    [Fact]
    public async Task A_suggested_actions_name_opens_its_node_panel_too()
    {
        var page = await Open();
        var key = page.Whole.Geometry!.Nodes[0].Key;

        page.Whole.OpenAction(key);
        await page.Whole.Panel.Reading;

        Assert.True(page.Whole.Panel.IsOpen);
        Assert.Equal(key, page.Whole.Panel.Key);
        Assert.Equal(key, page.Whole.Pinned);
    }
}
