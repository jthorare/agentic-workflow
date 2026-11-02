using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>A browser launch the system refuses from the node panel's open button says so, with the link to copy (#1712).</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelLaunchTests
{
    private const string Node = "feature:tabs.tab-manager";
    private const string Surface = "surface:command/switch-tab";
    private const string Link = "https://linear.app/x/issue/LIN-12";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private static readonly string _served = $$"""
        {
          "mapVersion": "v1",
          "node": { "key": "{{Node}}", "kind": "feature", "label": "Tab manager", "provenance": "human", "confirmed": true, "trust": "trusted", "confidence": 1, "source": { "kind": "ticket", "ref": "LIN-12", "url": "{{Link}}" } },
          "aliases": [],
          "out": [ { "from": "{{Node}}", "type": "touches", "to": "{{Surface}}", "provenance": "human", "confirmed": true } ],
          "in": []
        }
        """.ReplaceLineEndings("\n");
#pragma warning restore MA0136

    private static async Task<(ContextPageViewModel Page, FakeLauncher Launcher)> Open()
    {
        var api = new FakeSindriApi { NodeAnswer = new RouteAnswer(HttpStatusCode.OK, _served) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var launcher = new FakeLauncher();
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }, launcher));
        await page.Opened(Token);
        var rect = page.Whole.Geometry!.Nodes.First(n => n.Key == Node).Rect;
        page.Whole.PointerClicked(rect.CenterX, rect.CenterY);
        await page.Whole.Panel.Reading;
        return (page, launcher);
    }

    [Fact]
    public async Task A_refused_launch_says_the_link_could_not_be_opened_and_shows_the_link()
    {
        var (page, launcher) = await Open();
        launcher.Refuse = true;

        await page.Whole.Panel.OpenSource(Token);

        var panel = page.Whole.Panel;
        Assert.True(panel.LaunchFailed);
        Assert.Contains("Couldn't open the link", panel.LaunchFailedText, StringComparison.Ordinal);
        Assert.Contains(Link, panel.LaunchFailedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_launch_that_works_after_a_refused_one_clears_the_line()
    {
        var (page, launcher) = await Open();
        launcher.Refuse = true;
        await page.Whole.Panel.OpenSource(Token);
        Assert.True(page.Whole.Panel.LaunchFailed);

        launcher.Refuse = false;
        await page.Whole.Panel.OpenSource(Token);

        Assert.False(page.Whole.Panel.LaunchFailed);
        Assert.Equal([Link], launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Opening_another_node_clears_the_line()
    {
        var (page, launcher) = await Open();
        launcher.Refuse = true;
        await page.Whole.Panel.OpenSource(Token);
        Assert.True(page.Whole.Panel.LaunchFailed);

        page.Whole.Panel.Groups[0].Rows.First(r => string.Equals(r.Key, Surface, StringComparison.Ordinal)).Jump();
        await page.Whole.Panel.Reading;

        Assert.Equal(Surface, page.Whole.Panel.Key);
        Assert.False(page.Whole.Panel.LaunchFailed);
    }

    [Fact]
    public async Task A_launch_that_works_never_shows_the_line_but_a_refused_one_does()
    {
        var (page, launcher) = await Open();

        await page.Whole.Panel.OpenSource(Token);
        Assert.False(page.Whole.Panel.LaunchFailed);

        launcher.Refuse = true;
        await page.Whole.Panel.OpenSource(Token);
        Assert.True(page.Whole.Panel.LaunchFailed);
    }

    [Fact]
    public async Task A_refused_launch_raises_PropertyChanged_for_LaunchFailed()
    {
        var (page, launcher) = await Open();
        var raised = new List<string?>();
        page.Whole.Panel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        launcher.Refuse = true;

        await page.Whole.Panel.OpenSource(Token);

        Assert.Contains(nameof(NodePanelViewModel.LaunchFailed), raised, StringComparer.Ordinal);
    }
}
