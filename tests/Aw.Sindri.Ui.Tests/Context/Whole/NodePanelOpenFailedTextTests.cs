using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>When "Open file" fails, the panel says why in one sentence and never prints the absolute path (#3714); the link button reads "Open on GitHub ↗".</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelOpenFailedTextTests
{
    private const string Node = "feature:tabs.tab-manager";
    private const string LocalPath = "/work/r/src/a/File17.ts";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

#pragma warning disable MA0136 // a raw string with line ends is the readable form of this JSON
    private static readonly string _served = $$"""
        {
          "mapVersion": "v1",
          "node": { "key": "{{Node}}", "kind": "file", "label": "File17", "provenance": "human", "confirmed": true, "trust": "trusted", "confidence": 1, "source": { "kind": "file", "ref": "src/a/File17.ts", "path": "{{LocalPath}}", "url": "https://github.com/x/y/blob/main/src/a/File17.ts" } },
          "aliases": [],
          "out": [],
          "in": []
        }
        """.ReplaceLineEndings("\n");
#pragma warning restore MA0136

    private static async Task<(NodePanelViewModel Panel, FakeLauncher Launcher)> Open()
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
        return (page.Whole.Panel, launcher);
    }

    [Fact]
    public async Task A_file_that_is_not_on_this_machine_says_so_without_the_path()
    {
        var (panel, _) = await Open();

        await panel.OpenLocalFile(Token);

        Assert.True(panel.LaunchFailed);
        Assert.Equal("This file isn't on this machine.", panel.LaunchFailedText);
    }

    [Fact]
    public async Task A_file_that_exists_but_the_system_refuses_says_no_app_opens_its_extension()
    {
        var (panel, launcher) = await Open();
        launcher.AddFile(LocalPath);
        launcher.Refuse = true;

        await panel.OpenLocalFile(Token);

        Assert.True(panel.LaunchFailed);
        Assert.Equal("No app on this machine opens .ts files.", panel.LaunchFailedText);
    }

    [Fact]
    public async Task The_failed_text_never_holds_the_absolute_path_or_the_repository_hint()
    {
        var (panel, launcher) = await Open();
        await panel.OpenLocalFile(Token);
        var missing = panel.LaunchFailedText;
        launcher.AddFile(LocalPath);
        launcher.Refuse = true;
        await panel.OpenLocalFile(Token);

        Assert.DoesNotContain("/work/r", missing, StringComparison.Ordinal);
        Assert.DoesNotContain("/work/r", panel.LaunchFailedText, StringComparison.Ordinal);
        Assert.DoesNotContain("repository", missing + panel.LaunchFailedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_link_button_reads_open_on_github()
    {
        var (panel, _) = await Open();

        Assert.Equal(("Open file", "Open on GitHub ↗"), (panel.OpenFileText, panel.OpenSourceText));
    }
}
