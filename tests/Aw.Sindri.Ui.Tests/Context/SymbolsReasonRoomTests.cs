using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>
/// Why the symbols column is empty is a sentence, so it gets the note line under the table, never the column heading: at 1000 and
/// 1280 wide the heading cut it to "symbols · only C# files get names:…" (#3996). The bare dash cell says it on hover.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SymbolsReasonRoomTests
{
    private const string GraphOff = "only C# files get names: the code graph is off for this repo";

    [Fact]
    public async Task Graph_off_with_no_rows_keeps_the_heading_short_and_says_why_under_the_table()
    {
        var map = await Loaded("""{"state":"ok","reason":"graph-off","items":[],"commit":"7c3e1a2","files":[]}""");

        Assert.Equal("symbols · from 7c3e1a2", map.SymbolsHeading);
        Assert.Equal("symbols · " + GraphOff, map.Note);
    }

    [Fact]
    public async Task Graph_on_with_no_rows_says_none_in_these_files_under_the_table()
    {
        var map = await Loaded("""{"state":"ok","reason":null,"items":[],"commit":"7c3e1a2","files":[]}""");

        Assert.Equal("symbols · from 7c3e1a2", map.SymbolsHeading);
        Assert.Equal("symbols · none in these files", map.Note);
    }

    [Fact]
    public async Task A_dash_cell_with_the_graph_off_names_the_reason_on_hover()
    {
        var map = await Loaded("""{"state":"ok","reason":"graph-off","items":[],"commit":"7c3e1a2","files":[]}""");

        var file = map.Rows.First(static r => r.IsFile);

        Assert.Equal(("—", GraphOff), (file.SymbolsText, file.SymbolsTip));
    }

    [Fact]
    public async Task A_file_with_names_has_no_hover_reason()
    {
        var map = await Loaded("""{"state":"ok","reason":"graph-off","items":[],"commit":"7c3e1a2","files":[{"path":"src/tabs/TabManager.ts","symbols":[{"ref":"r1","name":"open","kind":"method","path":"src/tabs/TabManager.ts","startLine":1,"endLine":2,"signature":null,"exported":true}],"commit":"7c3e1a2","asOfCommit":null,"state":"ok"}]}""");

        var file = map.Rows.First(static r => string.Equals(r.Path, "src/tabs/TabManager.ts", StringComparison.Ordinal));

        Assert.Equal(("open", null), (file.SymbolsText, file.SymbolsTip));
        Assert.Equal(string.Empty, map.Note);
    }

    [Fact]
    public async Task An_index_at_another_commit_keeps_the_heading_short_and_names_both_commits_under_the_table()
    {
        var map = await Loaded("""{"state":"ok","reason":null,"items":[],"commit":"7c3e1a2","asOfCommit":"1234567","asOfMismatch":true,"files":[]}""");

        Assert.Equal("symbols · from 7c3e1a2", map.SymbolsHeading);
        Assert.Equal("symbols · from 7c3e1a2, not 1234567 · none in these files", map.Note);
    }

    private static async Task<FileMapViewModel> Loaded(string symbols)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        body["symbols"] = JsonNode.Parse(symbols);
        var api = new FakeSindriApi { FileMapAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString()) };
        var map = new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await map.Load("src/tabs/", TestContext.Current.CancellationToken);
        return map;
    }
}
