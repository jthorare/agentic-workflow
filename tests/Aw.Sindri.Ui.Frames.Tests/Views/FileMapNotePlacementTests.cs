using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The Files view's note line (#3862): it sits right under the table's last row, not pinned to the panel's bottom about 450 px below a one-row table.</summary>
[Lane(TestLane.Integration)]
public sealed class FileMapNotePlacementTests : FrameHost
{
    private const double RowHeight = 28;   // Row.Height.Compact (#2546)

    // A folder of n mapped files, and an index that answers with no names because the code graph is off: the note line has a sentence to show.
    private static RouteAnswer Folder(int n, string reason = "graph-off")
    {
        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        var entries = new JsonArray();
        for (var i = 0; i < n; i++)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"Tab{i:D2}.ts");
            entries.Add(new JsonObject { ["name"] = name, ["path"] = "src/tabs/" + name, ["type"] = "file", ["status"] = "mapped", ["files"] = 1, ["features"] = new JsonArray(), ["intent"] = null });
        }

        body["entries"] = entries;
        body["unmapped"] = new JsonArray();
        body["suggested"] = new JsonArray();
        body["totals"] = new JsonObject { ["files"] = n, ["mapped"] = n, ["suggested"] = 0, ["unmapped"] = 0 };
        body["total"] = n;
        body["symbols"] = JsonNode.Parse("{\"state\":\"ok\",\"reason\":\"" + reason + "\",\"items\":[],\"commit\":\"b3f29d6\",\"files\":[]}");
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    [Fact]
    public Task The_note_sits_right_under_a_one_row_table() => OnUi(async () =>
    {
        Server.FileMapAnswer = Folder(1);
        var (window, shell) = await OpenFiles();

        var row = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("file-row"));
        var note = Named<TextBlock>(window, "FilesNote");
        var rowBottom = row.TranslatePoint(new Point(0, row.Bounds.Height), window)?.Y ?? double.NaN;
        var noteTop = note.TranslatePoint(default, window)?.Y ?? double.NaN;

        Assert.False(string.IsNullOrEmpty(shell.Context.FileMap.Note));
        Assert.True(note.IsEffectivelyVisible, "the note line is hidden");
        Assert.True(noteTop >= rowBottom - 0.5 && noteTop - rowBottom <= RowHeight, string.Create(CultureInfo.InvariantCulture, $"the note's top is {noteTop:F1} px, the last row ends at {rowBottom:F1} px"));
    });

    // The other half of #3862: the graph is on in the profile but did not build, so the note names graphify and `sindri doctor`, not the profile key.
    [Fact]
    public Task The_note_for_a_graph_that_did_not_build_sits_under_the_table_and_names_doctor() => OnUi(async () =>
    {
        Server.FileMapAnswer = Folder(1, "graph-unavailable");
        var (window, shell) = await OpenFiles();

        var row = Assert.Single(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("file-row"));
        var note = Named<TextBlock>(window, "FilesNote");
        var rowBottom = row.TranslatePoint(new Point(0, row.Bounds.Height), window)?.Y ?? double.NaN;
        var noteTop = note.TranslatePoint(default, window)?.Y ?? double.NaN;

        Assert.Equal(Labels.ContextSymbolsGraphUnavailable, shell.Context.FileMap.Note);
        Assert.True(note.IsEffectivelyVisible, "the note line is hidden");
        Assert.True(noteTop >= rowBottom - 0.5 && noteTop - rowBottom <= RowHeight, string.Create(CultureInfo.InvariantCulture, $"the note's top is {noteTop:F1} px, the last row ends at {rowBottom:F1} px"));
        Frame(window, "files-graph-unavailable-1440", "f3-");
    });

    [Fact]
    public Task A_long_list_scrolls_and_the_note_stays_in_view_below_it() => OnUi(async () =>
    {
        Server.FileMapAnswer = Folder(80);
        var (window, _) = await OpenFiles();

        var scroll = Named<ScrollViewer>(window, "FileScroll");
        var note = Named<TextBlock>(window, "FilesNote");
        var scrollBottom = scroll.TranslatePoint(new Point(0, scroll.Bounds.Height), window)?.Y ?? double.NaN;
        var noteTop = note.TranslatePoint(default, window)?.Y ?? double.NaN;
        var noteBottom = noteTop + note.Bounds.Height;

        Assert.True(scroll.Extent.Height > scroll.Viewport.Height, "80 rows should scroll at 900 high");
        Assert.True(note.IsEffectivelyVisible, "the note line is hidden");
        Assert.True(noteTop >= scrollBottom - 0.5 && noteBottom <= window.Bounds.Height + 0.5, string.Create(CultureInfo.InvariantCulture, $"the note spans {noteTop:F1}..{noteBottom:F1} px, the list ends at {scrollBottom:F1} px, the window at {window.Bounds.Height:F1} px"));
    });

    private async Task<(ShellWindow Window, ShellViewModel Shell)> OpenFiles()
    {
        var (window, shell) = await Connected(width: 1440, height: 900);
        shell.GoTo(PageId.Context);
        Settle(window);
        await shell.Context.FileMap.Load("src/tabs/", Token);
        shell.Context.ChooseView(files: true);
        await Until(window, () => Visible(window, "FileRows"), "the file rows never drew: " + shell.Context.FileMap.Problem);
        Settle(window);
        return (window, shell);
    }
}
