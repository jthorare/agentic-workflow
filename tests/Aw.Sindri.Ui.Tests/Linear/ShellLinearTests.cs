using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Queue;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Linear;

/// <summary>The Linear view in the shell: L on Work, the C key, Settings from the Connect card, and reads only while Work is shown.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellLinearTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeClipboard _clipboard = new();

    private const string QueueRow = "ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301";

    // The golden queue with one row's primary action set to Decide, so Y has a command to copy.
    private static RouteAnswer WithCopyableRow()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(GoldenFiles.SpecShaped("queue", "extra-p4-waiting"))!;
        foreach (var item in root["groups"]!.AsArray().SelectMany(g => g!["items"]!.AsArray()).Where(i => string.Equals(i!["id"]!.GetValue<string>(), QueueRow, StringComparison.Ordinal)))
        {
            item!["primaryAction"] = "Decide";
        }

        return new RouteAnswer(System.Net.HttpStatusCode.OK, root.ToJsonString());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), new Preferences(new InMemoryPreferencesStore()), _clipboard);
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => shell.State == ShellState.Normal);
        return shell;
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(5, Token);
        }

        Assert.True(condition());
    }

    [Fact]
    public Task L_toggles_the_view_on_Work_and_does_nothing_elsewhere() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        var elsewhere = shell.Press("L");
        shell.GoTo(PageId.Work);
        var onWork = shell.Press("L");

        Assert.Equal((false, true, true), (elsewhere, onWork, shell.Linear.IsLinear));
    }, Token);

    [Fact]
    public Task C_opens_the_create_form_on_the_Sindri_view_and_does_nothing_yet_on_the_Linear_view() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Work);

        shell.Linear.Choose(linear: true);
        var onLinear = shell.Press("C");
        var openOnLinear = shell.Create.IsOpen;
        shell.Linear.Choose(linear: false);
        var onSindri = shell.Press("C");

        Assert.Equal((true, false, true, true), (onLinear, openOnLinear, onSindri, shell.Create.IsOpen));
    }, Token);

    [Fact]
    public Task Going_to_Work_on_the_Linear_view_reads_the_list_and_leaving_stops_reading() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.Linear.Choose(linear: true);
        await Task.Delay(50, Token);
        Assert.Empty(_server.LinearQueries);

        shell.GoTo(PageId.Work);
        await Until(() => !shell.Linear.IsLoading);

        Assert.Equal(["mine", "teams"], _server.LinearQueries.Select(q => q.Group).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task The_Connect_Linear_button_goes_to_Settings() => UiThread.Run(async () =>
    {
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var shell = await Shell();
        shell.GoTo(PageId.Work);
        shell.Linear.Choose(linear: true);
        await Until(() => shell.Linear.ShowsConnect);

        shell.Linear.RequestConnect();

        Assert.True(shell.IsSettings);
    }, Token);

    [Theory]
    [InlineData("Y")]
    [InlineData("C")]
    public Task Y_and_C_do_nothing_on_the_Linear_view(string key) => UiThread.Run(async () =>
    {
        _server.QueueView = WithCopyableRow();
        var shell = await Shell();
        await Until(() => shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0);
        shell.GoTo(PageId.Work);
        shell.SelectQueueRow(QueueRow);
        shell.Linear.Choose(linear: true);

        var handled = shell.Press(key);

        Assert.Equal((true, QueueRow, false, false, null), (handled, shell.Queue.SelectedId, shell.Queue.IsFiltering, shell.IsSheetOpen, _clipboard.Text));
    }, Token);

    private async Task<ShellViewModel> LinearShell()
    {
        _server.LinearIssuesFor = query => string.Equals(query.Group, "teams", StringComparison.Ordinal) ? RouteAnswer.Api("linear-issues", "teams-page2") : RouteAnswer.Api("linear-issues", "mine");
        _server.QueueView = WithCopyableRow();
        var shell = await Shell();
        await Until(() => shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0);
        shell.GoTo(PageId.Work);
        shell.Linear.Choose(linear: true);
        await Until(() => shell.Linear.Mine.Rows.Count > 0 && shell.Linear.Teams.Rows.Count > 0);
        return shell;
    }

    [Fact]
    public Task J_selects_the_first_row_and_then_moves_down_across_the_group_boundary() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();

        var first = shell.Press("J");
        var afterFirst = shell.Linear.SelectedKey;
        _ = shell.Press("J");
        _ = shell.Press("J");
        var last = shell.Linear.SelectedKey;
        _ = shell.Press("J");
        var across = shell.Linear.SelectedKey;

        Assert.Equal((true, "ENG-12", "OPS-40", "ENG-57"), (first, afterFirst, last, across));
    }, Token);

    [Fact]
    public Task K_moves_up_across_the_group_boundary_and_stops_at_the_first_row() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        for (var i = 0; i < 4; i++)
        {
            _ = shell.Press("J");
        }

        _ = shell.Press("K");
        var up = shell.Linear.SelectedKey;
        for (var i = 0; i < 10; i++)
        {
            _ = shell.Press("K");
        }

        Assert.Equal(("ENG-57", "ENG-12"), (up, shell.Linear.SelectedKey));
    }, Token);

    [Fact]
    public Task J_stops_at_the_last_row() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();

        for (var i = 0; i < 12; i++)
        {
            _ = shell.Press("J");
        }

        Assert.Equal("ENG-44", shell.Linear.SelectedKey);
    }, Token);

    [Fact]
    public Task The_selected_row_is_marked_and_the_one_it_left_is_not() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();

        _ = shell.Press("J");
        _ = shell.Press("J");

        Assert.Equal([false, true, false], shell.Linear.Mine.Rows.Select(r => r.IsSelected));
    }, Token);

    [Fact]
    public Task J_asks_the_view_to_focus_the_selected_row() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        var asked = new List<string>();
        shell.Linear.FocusRequested += (_, e) => asked.Add(e.ItemId);

        _ = shell.Press("J");

        Assert.Equal(["ENG-12"], asked, StringComparer.Ordinal);
    }, Token);

    [Fact]
    public Task Enter_raises_IssueRequested_with_the_selected_issue() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        var asked = new List<string>();
        shell.Linear.IssueRequested += (_, e) => asked.Add(e.Key);
        _ = shell.Press("J");
        _ = shell.Press("J");

        var handled = shell.Press("Enter");

        Assert.Equal((true, "ENG-31"), (handled, Assert.Single(asked)));
    }, Token);

    [Fact]
    public Task Enter_with_nothing_selected_opens_nothing() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        var asked = 0;
        shell.Linear.IssueRequested += (_, _) => asked++;

        var handled = shell.Press("Enter");

        Assert.Equal((true, 0), (handled, asked));
    }, Token);

    [Fact]
    public Task Slash_asks_the_view_to_focus_the_filter_bar_and_leaves_the_Sindri_filter_closed() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        var asked = 0;
        shell.Linear.FilterFocusRequested += (_, _) => asked++;

        var handled = shell.Press("/");

        Assert.Equal((true, 1, false), (handled, asked, shell.Queue.IsFiltering));
    }, Token);

    [Theory]
    [InlineData("J")]
    [InlineData("K")]
    [InlineData("Enter")]
    public Task The_row_keys_on_the_Linear_view_never_touch_the_Sindri_queue(string key) => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        shell.SelectQueueRow(QueueRow, QueueScope.Page);

        _ = shell.Press(key);

        Assert.Equal((QueueRow, false), (shell.Queue.SelectedId, shell.IsSheetOpen));
    }, Token);

    [Theory]
    [InlineData("J")]
    [InlineData("K")]
    [InlineData("Enter")]
    public Task The_row_keys_inside_a_sheet_do_not_move_the_Linear_selection(string key) => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        _ = shell.Press("J");
        var asked = 0;
        shell.Linear.IssueRequested += (_, _) => asked++;

        var handled = shell.Press(key, inSheet: true);

        Assert.Equal((false, "ENG-12", 0), (handled, shell.Linear.SelectedKey, asked));
    }, Token);

    [Fact]
    public Task J_and_Enter_on_the_Sindri_view_still_walk_the_queue() => UiThread.Run(async () =>
    {
        var shell = await LinearShell();
        shell.Linear.Choose(linear: false);

        _ = shell.Press("J");

        Assert.NotNull(shell.Queue.SelectedId);
    }, Token);

    [Fact]
    public Task The_row_keys_still_work_on_the_Sindri_view_of_Work() => UiThread.Run(async () =>
    {
        _server.QueueView = WithCopyableRow();
        var shell = await Shell();
        await Until(() => shell.Queue.RunningRows.Count + shell.Queue.QueuedRows.Count > 0);
        shell.GoTo(PageId.Work);

        Assert.True(shell.Press("/"));

        Assert.True(shell.Queue.IsFiltering);
    }, Token);

    [Fact]
    public Task The_stored_marker_is_restored_before_the_first_catch_up_read() => UiThread.Run(async () =>
    {
        var store = new InMemoryPreferencesStore();
        var preferences = new Preferences(store);
        await preferences.Update(p => p with { LastPage = "Work", WorkView = "Linear", LinearSince = "lin_marker" });
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, new FakeTimeProvider(A.Noon), preferences, _clipboard);

        await shell.Restore(Token);
        _ = shell.Run(Token);
        await Until(() => _server.LinearSinceAfters.Count > 0);

        Assert.All(_server.LinearSinceAfters, after => Assert.Equal("lin_marker", after));
    }, Token);
}
