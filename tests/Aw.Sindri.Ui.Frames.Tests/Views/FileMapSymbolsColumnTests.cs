using System.Net;
using System.Text.Json.Nodes;
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

/// <summary>The Files view's symbols column (S-9) in the window: a file row shows the names the code index gave, and a column the index cannot answer says why on every file row.</summary>
[Lane(TestLane.Integration)]
public sealed class FileMapSymbolsColumnTests : FrameHost
{
    private const string Managed = "src/tabs/TabManager.ts";

    private static RouteAnswer Served()
    {
        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        body["symbols"] = JsonNode.Parse("""{"state":"available","reason":null,"items":[],"commit":"7c3e1a2","files":[{"path":"src/tabs/TabManager.ts","commit":"7c3e1a2","asOfCommit":null,"state":"ok","symbols":[{"ref":"a","name":"setOrientation","kind":"method","path":"src/tabs/TabManager.ts","startLine":1,"endLine":2,"signature":null,"exported":true},{"ref":"b","name":"resize","kind":"method","path":"src/tabs/TabManager.ts","startLine":3,"endLine":4,"signature":null,"exported":true},{"ref":"c","name":"close","kind":"method","path":"src/tabs/TabManager.ts","startLine":5,"endLine":6,"signature":null,"exported":true}]}]}""");
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

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

    private static List<string> Cells(ShellWindow window) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => string.Equals(t.Name, "FileSymbols", StringComparison.Ordinal)).Select(t => t.Text ?? string.Empty)];

    [Fact]
    public Task A_file_row_shows_the_symbol_names_the_index_gave() => OnUi(async () =>
    {
        Server.FileMapAnswer = Served();
        var (window, shell) = await OpenFiles();

        var row = Assert.Single(shell.Context.FileMap.Rows, r => string.Equals(r.Path, Managed, StringComparison.Ordinal));
        Assert.Equal("setOrientation · resize · +1", row.SymbolsText);
        Assert.Contains(Cells(window), c => string.Equals(c, "setOrientation · resize · +1", StringComparison.Ordinal));
        Assert.Contains(row.SymbolsText, row.AutomationName, StringComparison.Ordinal);
    });

    [Fact]
    public Task A_column_the_index_cannot_answer_says_why_in_its_words() => OnUi(async () =>
    {
        var (window, _) = await OpenFiles();

        var cells = Cells(window);
        Assert.NotEmpty(cells);
        Assert.All(cells, c => Assert.Equal(Labels.ContextSymbolsNotBuilt, c));
    });
}
