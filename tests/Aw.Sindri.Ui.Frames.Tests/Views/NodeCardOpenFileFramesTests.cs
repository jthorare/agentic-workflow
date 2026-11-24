using System.Globalization;
using System.Net;
using Avalonia.Automation;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>
/// The lens node card's Open file in the window (#4220): a code file whose repo has a checkout here shows Open file beside Copy path, in the whole map
/// panel's words. Saved under frames/ as node-card-open-file-*.png when AW_UI_FRAMES=1, before the checks.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class NodeCardOpenFileFramesTests : FrameHost
{
    private const string ManagerFile = "code:src/tabs/TabManager.ts";

    private const string Served = """
        { "mapVersion": "v1", "node": { "key": "code:src/tabs/TabManager.ts", "kind": "code", "label": "TabManager.ts", "provenance": "deterministic", "confirmed": true, "trust": "trusted", "confidence": 1, "source": { "kind": "file", "ref": "src/tabs/TabManager.ts", "path": "/work/aw-split-panes/src/tabs/TabManager.ts" } }, "aliases": [], "out": [], "in": [] }
        """;

    [Theory]
    [InlineData(1280)]
    [InlineData(1000)]
    public Task A_code_file_on_this_machine_shows_Open_file_beside_Copy_path(double width) => OnUi(async () =>
    {
        Server.NodeAnswer = new RouteAnswer(HttpStatusCode.OK, Served);
        var (window, shell) = await Connected(width: width, height: 900);
        shell.GoTo(PageId.Context);
        Settle(window);
        shell.Context.AnchorText = (string)GoldenFiles.Node("context-lenses", "map-tabs")["anchor"]!["key"]!;
        await shell.Context.Submit(Token);
        await shell.Context.Select(ManagerFile, Token);
        await Until(window, () => Visible(window, "NodeHost") && shell.Context.Node.HasOpenFile, "the node card never offered Open file: " + shell.Context.Node.Problem);
        Settle(window);
        Frame(window, string.Create(CultureInfo.InvariantCulture, $"node-card-open-file-{width:0}"), prefix: "");

        var open = Named<Button>(window, "NodeOpenFile");

        Assert.True(Visible(window, "NodeOpenFile"));
        Assert.True(Visible(window, "NodeCopyPath"));
        Assert.Equal(Labels.ContextNodePanelOpenFile, open.Content as string);
        Assert.Equal(Labels.ContextNodePanelOpenFileName, AutomationProperties.GetName(open));
        Assert.False(Visible(window, "NodeLaunchFailed"));
    });
}
