using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context.Node;

/// <summary>
/// The lens node card's Open file (#4220): a code file whose repo has a checkout here gets the whole map panel's button (same words, the launcher's file open),
/// a node the read serves no file for has none, and a refused open reads in the card's launch-failed line.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class NodeCardOpenFileTests
{
    private const string Manager = "feature:tabs.tab-manager";
    private const string ManagerFile = "code:src/tabs/TabManager.ts";
    private const string Path = "/work/aw-split-panes/src/tabs/TabManager.ts";
    private const string OnDisk = $$"""{ "kind": "file", "ref": "src/tabs/TabManager.ts", "path": "{{Path}}" }""";
    private const string NotOnDisk = """{ "kind": "file", "ref": "src/tabs/TabManager.ts" }""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Served(string source) => $$"""
        { "mapVersion": "v1", "node": { "key": "{{ManagerFile}}", "kind": "code", "label": "TabManager.ts", "provenance": "deterministic", "confirmed": true, "trust": "trusted", "confidence": 1, "source": {{source}} }, "aliases": [], "out": [], "in": [] }
        """;

    private static async Task<(NodeCardViewModel Card, FakeLauncher Launcher)> Open(string source, bool onMachine = true)
    {
        var api = new FakeSindriApi { NodeAnswer = new RouteAnswer(HttpStatusCode.OK, Served(source)) };
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var launcher = new FakeLauncher();
        if (onMachine)
        {
            launcher.AddFile(Path);
        }

        var page = new ContextPageViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }, launcher)) { AnchorText = Manager };
        await page.Submit(Token);
        await page.Select(ManagerFile, Token);
        return (page.Node, launcher);
    }

    [Fact]
    public async Task A_code_file_with_a_checkout_shows_Open_file_and_it_opens_the_file()
    {
        var (card, launcher) = await Open(OnDisk);

        Assert.True(card.HasOpenFile);
        Assert.Equal(Labels.ContextNodePanelOpenFile, card.OpenFileText);
        Assert.Equal(Labels.ContextNodePanelOpenFileName, card.OpenFileName);

        await card.OpenLocalFile(Token);

        Assert.Equal([Path], launcher.Opened, StringComparer.Ordinal);
        Assert.False(card.LaunchFailed);
    }

    [Fact]
    public async Task A_node_with_no_file_on_this_machine_has_no_Open_file()
    {
        var (card, launcher) = await Open(NotOnDisk);

        Assert.False(card.HasOpenFile);

        await card.OpenLocalFile(Token);

        Assert.Empty(launcher.Opened);
    }

    [Fact]
    public async Task A_refused_open_reads_in_the_launch_failed_line()
    {
        var (card, launcher) = await Open(OnDisk, onMachine: false);

        await card.OpenLocalFile(Token);

        Assert.Empty(launcher.Opened);
        Assert.True(card.LaunchFailed);
        Assert.Equal(Labels.FileNotOnMachine, card.LaunchFailedText);
    }
}
