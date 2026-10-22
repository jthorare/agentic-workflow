using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context;
using Aw.Sindri.Ui.ViewModels.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Context;

/// <summary>The Files view's rows (F3 spec 1, slice 4): the context-filemap goldens turned into rows, folders opened and left, and the refusals said in words.</summary>
[Lane(TestLane.Unit)]
public sealed class FileMapViewModelTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static FileMapViewModel Files(FakeSindriApi api)
    {
        var clipboard = new FakeClipboard();
        var clock = new FakeTimeProvider(A.Noon);
        return new FileMapViewModel(new ContextPorts(api, () => new Uri("http://127.0.0.1:1/"), clipboard, clock, new CatalogViewModel(clipboard, clock), _ => { }));
    }

    private static System.Text.Json.Nodes.JsonNode Golden(string scenario) => GoldenFiles.Node("context-filemap", scenario);

    [Fact]
    public async Task The_root_lists_every_entry_of_the_golden_with_its_files_and_its_tickets_and_decisions()
    {
        var golden = Golden("root");
        var files = Files(new FakeSindriApi());

        await files.Load(path: null, Token);

        Assert.Equal(golden["entries"]!.AsArray().Select(e => (string)e!["name"]!), files.Rows.Select(r => r.Name), StringComparer.Ordinal);
        var src = files.Rows[0];
        Assert.Equal((true, Labels.ContextFilesCount(4), Labels.ContextFilesIntent(records: 1, decisions: 0)), (src.IsDirectory, src.FilesText, src.IntentText));
        var readme = files.Rows[1];
        Assert.Equal((false, Labels.ContextFilesIntentNone), (readme.IsDirectory, readme.IntentText));
        Assert.Equal(Labels.ContextFilesRoot, files.PathText);
        Assert.False(files.CanGoUp);
    }

    [Fact]
    public async Task A_suggested_file_is_hatched_and_says_how_sure_the_tool_is_while_a_mapped_one_is_plain()
    {
        var files = Files(new FakeSindriApi());

        await files.Load("src/tabs/", Token);

        var suggested = Assert.Single(files.Rows, r => r.IsSuggested);
        Assert.Equal("TabGroups.ts", suggested.Name);
        Assert.Contains(Labels.ContextMapSuggestedPct(74), suggested.ForText, StringComparison.Ordinal);
        Assert.Contains("tabs.tab-groups", suggested.ForText, StringComparison.Ordinal);
        Assert.Equal(Labels.ContextFilesIntent(records: 0, decisions: 2), suggested.IntentText);
        var mapped = Assert.Single(files.Rows, r => string.Equals(r.Name, "TabManager.ts", StringComparison.Ordinal));
        Assert.False(mapped.IsSuggested);
        Assert.Equal(Labels.ContextMapInMap, mapped.StatusWord);
        Assert.DoesNotContain("%", mapped.ForText, StringComparison.Ordinal);
        var pinned = Assert.Single(files.Rows, r => string.Equals(r.Name, "Pinned.ts", StringComparison.Ordinal));
        Assert.Equal((false, Labels.ContextFilesNoFeature), (pinned.IsSuggested, pinned.ForText));
    }

    [Fact]
    public async Task The_totals_line_and_the_function_names_note_come_from_the_golden()
    {
        var golden = Golden("src-tabs");
        var files = Files(new FakeSindriApi());

        await files.Load("src/tabs/", Token);

        var totals = golden["totals"]!;
        Assert.Equal(Labels.ContextFilesTotals((int)totals["files"]!, (int)totals["mapped"]!, (int)totals["suggested"]!, (int)totals["unmapped"]!), files.TotalsText);
        Assert.Equal(Labels.ContextMapNamesDetail, files.Note);
        Assert.True(files.HasNote);
    }

    [Fact]
    public async Task Opening_a_folder_reads_it_and_up_goes_back_one_level_to_the_root()
    {
        var files = Files(new FakeSindriApi());
        await files.Load(path: null, Token);

        await files.Open(files.Rows.First(r => r.IsDirectory), Token);
        Assert.Equal(("src/", true), (files.PathText, files.CanGoUp));
        await files.Open(files.Rows.First(r => r.IsDirectory && string.Equals(r.Name, "tabs", StringComparison.Ordinal)), Token);
        Assert.Equal("src/tabs/", files.PathText);

        await files.Up(Token);
        Assert.Equal("src/", files.PathText);
        await files.Up(Token);
        Assert.Equal((Labels.ContextFilesRoot, false), (files.PathText, files.CanGoUp));
    }

    [Fact]
    public async Task Opening_a_file_reads_nothing()
    {
        var server = new FakeSindriApi();
        var files = Files(server);
        await files.Load("src/tabs/", Token);
        var calls = server.ContextCalls;

        await files.Open(files.Rows.First(r => !r.IsDirectory), Token);

        Assert.Equal(calls, server.ContextCalls);
    }

    [Fact]
    public async Task A_refusal_is_said_in_the_served_words_and_leaves_no_rows()
    {
        var server = new FakeSindriApi { FileMapAnswer = RouteAnswer.Problem("context-filemap", "repo-outside-045") };
        var files = Files(server);

        await files.Load(path: null, Token);

        Assert.Empty(files.Rows);
        Assert.True(files.HasProblem);
        Assert.Contains("this caller can read", files.Problem, StringComparison.Ordinal);
        Assert.False(files.HasRows);
    }

    [Fact]
    public async Task A_read_still_in_flight_does_not_draw_over_a_newer_one()
    {
        var gate = new TaskCompletionSource();
        var server = new FakeSindriApi { FileMapGate = gate };
        var files = Files(server);
        var slow = files.Load("src/", Token);
        server.FileMapGate = null;

        await files.Load("src/tabs/", Token);
        gate.SetResult();
        await slow;

        Assert.Equal("src/tabs/", files.PathText);
    }
}
