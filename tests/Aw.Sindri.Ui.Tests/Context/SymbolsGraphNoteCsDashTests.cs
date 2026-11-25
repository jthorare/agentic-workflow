using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>
/// With the code graph off, the "other languages need the code graph" sentence is true only of a dash on a file the C# parser does not read.
/// A .cs file with no functions also shows a dash, but the parser read it and found nothing: no graph sentence, in the note or on hover (#4418).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class SymbolsGraphNoteCsDashTests
{
    private const string GraphOff = "C# names come from the index. Other languages need the code graph, which is off for this repo (index.graph.enabled in the profile).";

    private const string AMethod = """{"path":"src/tabs/A.cs","symbols":[{"ref":"r1","name":"Run","kind":"method","path":"src/tabs/A.cs","startLine":1,"endLine":2,"signature":null,"exported":true}],"commit":"7c3e1a2","asOfCommit":null,"state":"ok"}""";

    [Fact]
    public async Task A_cs_file_with_no_functions_gets_no_graph_sentence_with_the_graph_off()
    {
        var map = await Loaded(["A.cs", "Empty.cs"], AMethod, """{"path":"src/tabs/Empty.cs","symbols":[],"commit":"7c3e1a2","asOfCommit":null,"state":"ok"}""");

        var empty = map.Rows.First(static r => string.Equals(r.Path, "src/tabs/Empty.cs", StringComparison.Ordinal));

        Assert.Equal(("—", false, false), (empty.SymbolsText, map.Note.Contains("graph", StringComparison.Ordinal), string.Equals(empty.SymbolsTip, GraphOff, StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_ts_file_with_no_symbols_still_gets_the_graph_sentence_with_the_graph_off()
    {
        var map = await Loaded(["A.cs", "Old.ts"], AMethod);

        var old = map.Rows.First(static r => string.Equals(r.Path, "src/tabs/Old.ts", StringComparison.Ordinal));

        Assert.Equal((GraphOff, "—", GraphOff), (map.Note, old.SymbolsText, old.SymbolsTip));
    }

    private static async Task<FileMapViewModel> Loaded(string[] names, params string[] files)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        body["entries"] = new JsonArray([.. names.Select(static n => JsonNode.Parse($$"""{"name":"{{n}}","path":"src/tabs/{{n}}","type":"file","status":"unmapped","files":1,"features":[],"intent":null}"""))]);
        body["symbols"] = JsonNode.Parse($$"""{"state":"ok","reason":"graph-off","items":[],"commit":"7c3e1a2","files":[{{string.Join(',', files)}}]}""");
        var api = new FakeSindriApi { FileMapAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString()) };
        var map = new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        await map.Load("src/tabs/", TestContext.Current.CancellationToken);
        return map;
    }
}
