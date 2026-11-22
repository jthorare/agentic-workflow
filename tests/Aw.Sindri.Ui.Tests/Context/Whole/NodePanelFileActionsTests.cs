using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>The node panel's file actions (#3715): Enter opens the local file before the GitHub link, "Open source" is the link only, and "Copy path" copies the file's path.</summary>
[Lane(TestLane.Unit)]
public sealed class NodePanelFileActionsTests
{
    private const string Node = "feature:tabs.tab-manager";
    private const string Path = "/work/r/src/a/File17.ts";
    private const string Link = "https://github.com/acme/r/blob/abc/src/a/File17.ts";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Served(string source) => $$"""
        { "mapVersion": "v1", "node": { "key": "{{Node}}", "kind": "file", "label": "File17", "provenance": "human", "confirmed": true, "trust": "trusted", "confidence": 1, "source": {{source}} }, "aliases": [], "out": [], "in": [] }
        """;

    private static async Task<(ContextPageViewModel Page, FakeLauncher Launcher, FakeClipboard Clipboard)> Open(string source)
    {
        var api = new FakeSindriApi { NodeAnswer = new RouteAnswer(HttpStatusCode.OK, Served(source)) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var catalog = new CatalogViewModel(clipboard, clock);
        var url = new Uri("http://127.0.0.1:1/");
        catalog.Show(url, api, (await api.Commands(url, Token)).Value);
        var launcher = new FakeLauncher();
        launcher.AddFile(Path);
        var page = new ContextPageViewModel(new ContextPorts(api, () => url, clipboard, clock, catalog, _ => { }, launcher));
        await page.Opened(Token);
        var rect = page.Whole.Geometry!.Nodes.First(n => n.Key == Node).Rect;
        page.Whole.PointerClicked(rect.CenterX, rect.CenterY);
        await page.Whole.Panel.Reading;
        return (page, launcher, clipboard);
    }

    private static readonly string _both = $$"""{ "kind": "file", "ref": "src/a/File17.ts", "url": "{{Link}}", "path": "{{Path}}" }""";

    [Fact]
    public async Task Enter_opens_the_local_file_when_the_node_has_one_even_with_a_link()
    {
        var (page, launcher, _) = await Open(_both);

        await page.Whole.Panel.OpenSource(Token);

        Assert.Equal([Path], launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Enter_opens_the_GitHub_link_when_there_is_no_local_file()
    {
        var (page, launcher, _) = await Open($$"""{ "kind": "file", "ref": "src/a/File17.ts", "url": "{{Link}}" }""");

        await page.Whole.Panel.OpenSource(Token);

        Assert.Equal([Link], launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_open_source_button_opens_the_link_only()
    {
        var (page, launcher, _) = await Open(_both);

        await page.Whole.Panel.OpenLink(Token);

        Assert.Equal([Link], launcher.Opened, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Copy_path_puts_the_file_path_on_the_clipboard()
    {
        var (page, _, clipboard) = await Open(_both);

        await page.Whole.Panel.CopyPath(Token);

        Assert.Equal(Path, clipboard.Text);
        Assert.False(page.Whole.Panel.LaunchFailed);
    }

    [Fact]
    public async Task A_refused_copy_says_so_and_shows_the_path()
    {
        var (page, _, clipboard) = await Open(_both);
        clipboard.Refuse = true;

        await page.Whole.Panel.CopyPath(Token);

        Assert.True(page.Whole.Panel.LaunchFailed);
        Assert.Contains(Path, page.Whole.Panel.LaunchFailedText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_node_without_a_local_file_copies_nothing()
    {
        var (page, _, clipboard) = await Open($$"""{ "kind": "file", "ref": "src/a/File17.ts", "url": "{{Link}}" }""");

        await page.Whole.Panel.CopyPath(Token);

        Assert.False(page.Whole.Panel.HasOpenFile);
        Assert.Null(clipboard.Text);
    }
}
