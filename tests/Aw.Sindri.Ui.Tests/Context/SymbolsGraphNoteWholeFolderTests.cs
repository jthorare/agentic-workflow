using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The graph sentence follows one per-path rule, also for a folder with no symbol rows at all and for the "did not build" reason (#4418).</summary>
[Lane(TestLane.Unit)]
public sealed class SymbolsGraphNoteWholeFolderTests
{
    private const string GraphOff = "C# names come from the index. Other languages need the code graph, which is off for this repo (index.graph.enabled in the profile).";

    [Fact]
    public async Task A_folder_of_only_cs_files_with_no_rows_says_none_not_the_graph_sentence()
    {
        var map = await Loaded("graph-off", ["A.cs", "B.cs"]);

        Assert.Equal("Symbols · none in these files", map.Note);
    }

    [Fact]
    public async Task A_folder_with_a_ts_file_and_no_rows_keeps_the_graph_sentence()
    {
        var map = await Loaded("graph-off", ["A.cs", "Old.ts"]);

        Assert.Equal(GraphOff, map.Note);
    }

    [Fact]
    public async Task A_ts_dash_gets_the_graph_hover_and_a_cs_dash_gets_none()
    {
        var map = await Loaded("graph-off", ["A.cs", "Old.ts"]);

        var tips = map.Rows.Where(static r => r.IsFile).ToDictionary(static r => r.Path, static r => r.SymbolsTip, StringComparer.Ordinal);

        Assert.Equal((null, GraphOff), (tips["src/tabs/A.cs"], tips["src/tabs/Old.ts"]));
    }

    [Fact]
    public async Task A_cs_only_dash_with_the_graph_down_gets_no_did_not_build_sentence()
    {
        var map = await Loaded("graph-unavailable", ["A.cs"]);

        Assert.DoesNotContain("did not build", map.Note, StringComparison.Ordinal);
    }

    private static async Task<FileMapViewModel> Loaded(string reason, string[] names)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        body["entries"] = new JsonArray([.. names.Select(static n => JsonNode.Parse($$"""{"name":"{{n}}","path":"src/tabs/{{n}}","type":"file","status":"unmapped","files":1,"features":[],"intent":null}"""))]);
        body["symbols"] = JsonNode.Parse($$"""{"state":"ok","reason":"{{reason}}","items":[],"commit":"7c3e1a2","files":[]}""");
        var api = new FakeSindriApi { FileMapAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString()) };
        var map = new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await map.Load("src/tabs/", TestContext.Current.CancellationToken);
        return map;
    }
}
