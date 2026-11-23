using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The code index's words on the file map's symbols column and the code lens' callers and callees panels (S-9, code index spec §7.3, §10).</summary>
[Lane(TestLane.Unit)]
public sealed class SymbolsColumnTests
{
    private const string Indexed = "7c3e1a2f4b5d6e7081920a1b2c3d4e5f60718293";
    private const string Asked = "1234567890abcdef1234567890abcdef12345678";

    [Fact]
    public void A_fresh_index_reads_from_its_commit_and_each_file_lists_two_names_and_a_count()
    {
        var column = new SymbolsColumnViewModel(Served("ok", asOf: null, mismatch: false, Row("src/layout/SplitPane.ts", "setOrientation", "resize", "a", "b", "c")));

        Assert.Equal(("from 7c3e1a2", false), (column.Caption, column.Dimmed));
        Assert.Equal("setOrientation · resize · +3", column.Cell("src/layout/SplitPane.ts"));
        Assert.Equal("—", column.Cell("src/unknown.ts"));
    }

    [Fact]
    public void An_ok_index_with_graph_off_and_no_rows_says_only_csharp_gets_names()
    {
        var column = new SymbolsColumnViewModel(Served("ok", asOf: null, mismatch: false) with { Reason = "graph-off" });

        Assert.Equal(("from 7c3e1a2", "only C# files get names: the code graph is off in the profile (index.graph.enabled)", false), (column.Caption, column.EmptyReason, column.Dimmed));
        Assert.Equal("—", column.Cell("src/tabs/TabManager.ts"));
    }

    // #3862: a graph layer that is down is not one the profile turned off; the words name the other fix.
    [Fact]
    public void An_ok_index_with_the_graph_down_and_no_rows_says_graphify_is_missing_or_failed()
    {
        var column = new SymbolsColumnViewModel(Served("ok", asOf: null, mismatch: false) with { Reason = "graph-unavailable" });

        Assert.Equal(("from 7c3e1a2", "only C# files get names: the code graph did not build, graphify is missing or failed (sindri doctor says which)", "only C# files get names: the code graph did not build, graphify is missing or failed (sindri doctor says which)"), (column.Caption, column.EmptyReason, column.NoRowTip));
    }

    [Fact]
    public void An_ok_index_with_graph_on_and_no_rows_says_none_in_these_files()
    {
        var column = new SymbolsColumnViewModel(Served("ok", asOf: null, mismatch: false));

        Assert.Equal(("from 7c3e1a2", "none in these files", false), (column.Caption, column.EmptyReason, column.Dimmed));
        Assert.Equal("—", column.Cell("src/tabs/TabManager.ts"));
    }

    [Fact]
    public void Graph_off_with_rows_keeps_the_commit_caption()
    {
        var column = new SymbolsColumnViewModel(Served("ok", asOf: null, mismatch: false, Row("src/a.cs", "Save")) with { Reason = "graph-off" });

        Assert.Equal(("from 7c3e1a2", null), (column.Caption, column.EmptyReason));
    }

    [Fact]
    public void A_file_with_two_symbols_has_no_count()
    {
        var column = new SymbolsColumnViewModel(Served("ok", asOf: null, mismatch: false, Row("src/a.ts", "save", "load")));

        Assert.Equal("save · load", column.Cell("src/a.ts"));
    }

    [Fact]
    public void An_index_at_another_commit_than_the_maps_names_both_commits_and_dims_the_cells()
    {
        var column = new SymbolsColumnViewModel(Served("stale", Asked, mismatch: true, Row("src/a.ts", "save")));

        Assert.Equal(("from 7c3e1a2", "from 7c3e1a2, not 1234567", true), (column.Caption, column.AsOfNote, column.Dimmed));
        Assert.Equal("save", column.Cell("src/a.ts"));
    }

    [Fact]
    public void A_stale_index_with_no_other_map_commit_says_stale()
    {
        var column = new SymbolsColumnViewModel(Served("stale", asOf: null, mismatch: false));

        Assert.Equal(("from 7c3e1a2, stale", true), (column.Caption, column.Dimmed));
    }

    [Theory]
    [InlineData("index-not-built", "index not built")]
    [InlineData("index not built", "index not built")]
    [InlineData("index-damaged", "index damaged")]
    [InlineData("index-building", "index building")]
    [InlineData("not-configured", "not configured")]
    [InlineData(null, "index not built")]
    public void An_index_that_cannot_answer_says_why_in_every_cell(string? reason, string words)
    {
        var column = new SymbolsColumnViewModel(new SymbolsView("unavailable", reason, []));

        Assert.True(column.Unavailable);
        Assert.Equal(words, column.Caption);
        Assert.Equal(words, column.Cell("src/a.ts"));
    }

    [Fact]
    public async Task The_heading_says_where_the_names_came_from_unless_the_index_cannot_answer()
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var api = new FakeSindriApi();
        var map = new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));

        await map.Load("src/tabs/", TestContext.Current.CancellationToken);
        Assert.Equal("symbols", map.SymbolsHeading);

        var body = GoldenFiles.Node("context-filemap", "src-tabs");
        body["symbols"] = System.Text.Json.Nodes.JsonNode.Parse("""{"state":"stale","reason":null,"items":[],"commit":"7c3e1a2","files":[]}""");
        api.FileMapAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, body.ToJsonString());
        await map.Load("src/tabs/", TestContext.Current.CancellationToken);
        Assert.Equal("symbols · from 7c3e1a2, stale", map.SymbolsHeading);
    }

    [Fact]
    public async Task The_file_map_takes_its_column_from_the_answer()
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var map = new FileMapViewModel(new ContextPorts(new FakeSindriApi(), () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));

        await map.Load("src/tabs/", TestContext.Current.CancellationToken);

        Assert.Equal("index not built", map.Symbols!.Caption);
    }

    [Fact]
    public void Callers_list_each_edge_with_how_the_index_knows_it_and_count_the_calls_not_resolved()
    {
        var panel = new CallsPanelViewModel(callers: true, Calls("ok", unresolved: 3, """{"symbol":{"name":"restoreLayout","path":"src/tabs/Manager.ts"},"basis":"syntax","coverage":"full","depth":1}"""));

        Assert.Equal(("Callers", "from 7c3e1a2", "3 calls not resolved"), (panel.Title, panel.Caption, panel.Unresolved));
        Assert.Equal(new CallRow("restoreLayout", "src/tabs/Manager.ts", "syntax · full", 1), Assert.Single(panel.Rows));
        Assert.Null(panel.Empty);
    }

    [Fact]
    public void One_call_not_resolved_is_singular_and_none_is_not_said()
    {
        Assert.Equal("1 call not resolved", new CallsPanelViewModel(callers: false, Calls("ok", unresolved: 1)).Unresolved);
        Assert.Null(new CallsPanelViewModel(callers: false, Calls("ok", unresolved: 0)).Unresolved);
    }

    [Fact]
    public void An_answer_with_no_edges_says_none_found_and_a_stale_one_names_both_commits()
    {
        var panel = new CallsPanelViewModel(callers: false, Calls("stale", unresolved: 0));

        Assert.Equal(("Callees", "from 7c3e1a2, not 1234567", true, "none found"), (panel.Title, panel.Caption, panel.Dimmed, panel.Empty));
    }

    [Fact]
    public void An_unavailable_index_shows_no_rows_and_no_unresolved_count()
    {
        var panel = new CallsPanelViewModel(callers: true, new SymbolsView("unavailable", "index-not-built", [], Unresolved: 4));

        Assert.Equal(("index not built", true, 0), (panel.Caption, panel.Unavailable, panel.Rows.Count));
        Assert.Null(panel.Unresolved);
        Assert.Null(panel.Empty);
    }

    [Fact]
    public void The_code_lens_holds_both_panels_once_an_answer_is_shown()
    {
        var lens = new CodeLensViewModel();
        Assert.Null(lens.Callers);

        lens.Show(Calls("ok", unresolved: 0), Calls("ok", unresolved: 2));

        Assert.Equal(("Callers", "Callees", "2 calls not resolved"), (lens.Callers!.Title, lens.Callees!.Title, lens.Callees.Unresolved));
    }

    [Fact]
    public void A_row_captions_its_file_and_how_the_index_knows_it_and_a_dimmed_panel_draws_at_reduced_opacity()
    {
        var stale = new CallsPanelViewModel(callers: true, Calls("stale", unresolved: 0, """{"symbol":{"name":"a","path":"src/a.ts"},"basis":"graphify","coverage":"partial","depth":1}"""));
        var fresh = new CallsPanelViewModel(callers: true, Calls("ok", unresolved: 0));

        Assert.Equal("src/a.ts · graphify · partial", Assert.Single(stale.Rows).Caption);
        Assert.Equal((0.6, 1.0), (stale.Opacity, fresh.Opacity));
        Assert.Equal((false, true), (stale.HasEmpty, fresh.HasEmpty));
    }

    [Fact]
    public void The_code_lens_raises_its_changes_when_an_answer_is_shown()
    {
        var lens = new CodeLensViewModel();
        var raised = new List<string?>();
        lens.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        Assert.False(lens.HasPanels);

        lens.Show(Calls("ok", unresolved: 0), Calls("ok", unresolved: 0));

        Assert.True(lens.HasPanels);
        Assert.Equal([nameof(CodeLensViewModel.Callers), nameof(CodeLensViewModel.Callees), nameof(CodeLensViewModel.HasPanels)], raised);
    }

    [Fact]
    public async Task Reading_the_code_lens_fills_both_panels_and_a_refusal_leaves_them_alone()
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        var page = new ContextPageViewModel(new ContextPorts(new FakeSindriApi(), () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
        var key = (string)GoldenFiles.Node("context-code", "edges-and-lexical")["anchors"]![0]!["key"]!;

        Assert.False(await page.ReadCode(["feature:nope"], draft: false, TestContext.Current.CancellationToken));
        Assert.Null(page.Code.Callers);
        Assert.True(await page.ReadCode([key], draft: false, TestContext.Current.CancellationToken));
        Assert.Equal("index not built", page.Code.Callers!.Caption);
    }

    private static SymbolsView Served(string state, string? asOf, bool mismatch, params FileSymbolsEntry[] files) =>
        new(state, Reason: null, [], Commit: Indexed, AsOfCommit: asOf, AsOfMismatch: mismatch, Files: files);

    private static SymbolsView Calls(string state, int unresolved, params string[] items) =>
        new(state, Reason: null, [.. items.Select(static i => JsonDocument.Parse(i).RootElement.Clone())], Commit: Indexed, AsOfCommit: string.Equals(state, "stale", StringComparison.Ordinal) ? Asked : null, AsOfMismatch: string.Equals(state, "stale", StringComparison.Ordinal), Unresolved: unresolved);

    private static FileSymbolsEntry Row(string path, params string[] names) =>
        new(path, [.. names.Select(n => new SymbolSiteView(path + "#" + n, n, "method", path, 1, 2, Signature: null, Exported: true))], Indexed, AsOfCommit: null, "ok");
}
