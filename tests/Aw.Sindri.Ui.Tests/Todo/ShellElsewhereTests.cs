using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Todo;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The shell's side of the Todo panel's other places (todo spec §3, §5): the From elsewhere line's reads and links, G T, the palette's Todo row and the tray's todo counts.</summary>
[Lane(TestLane.Unit)]
public sealed class ShellElsewhereTests
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new() { Catalog = RouteAnswer.Api("commands", "catalog"), NeedsYouView = RouteAnswer.Api("needs-you", "empty") };
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-01-01T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<ShellViewModel> Shell()
    {
        _records.Write(A.Record());
        var shell = new ShellViewModel(_records, _server, _clock, new Preferences(new InMemoryPreferencesStore()), new FakeClipboard());
        await shell.Restore(Token);
        _ = shell.Run(Token);
        await ShellWait.Until(shell, () => shell.State == ShellState.Normal, Token);
        return shell;
    }

    [Fact]
    public Task The_line_reads_the_reviews_count_and_the_assigned_linear_count() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        await ShellWait.Until(shell, () => !shell.Elsewhere.IsLoading, Token);

        Assert.Equal("2 PRs waiting on your review · 3 Linear issues assigned", shell.Elsewhere.Text);
    }, Token);

    [Fact]
    public Task The_line_counts_the_needs_you_rows_below() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = RouteAnswer.Api("needs-you", "fixture");
        var shell = await Shell();

        await ShellWait.Until(shell, () => shell.Elsewhere.Text.Contains("need you, below", StringComparison.Ordinal), Token);

        Assert.Contains(shell.Elsewhere.Parts, p => p.Part == ElsewherePart.NeedsYou);
    }, Token);

    [Fact]
    public Task The_reviews_part_opens_Reviews() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        await ShellWait.Until(shell, () => !shell.Elsewhere.IsLoading, Token);

        shell.Elsewhere.Open(ElsewherePart.Reviews);

        Assert.Equal(PageId.Reviews, shell.CurrentPage);
    }, Token);

    [Fact]
    public Task The_linear_part_opens_Work_on_the_Linear_view() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        await ShellWait.Until(shell, () => !shell.Elsewhere.IsLoading, Token);

        shell.Elsewhere.Open(ElsewherePart.Linear);

        Assert.Equal(PageId.Work, shell.CurrentPage);
        Assert.True(shell.Linear.IsLinear);
    }, Token);

    [Fact]
    public Task Linear_not_connected_opens_Settings() => UiThread.Run(async () =>
    {
        _server.LinearIssuesAnswer = RouteAnswer.Api("linear-issues", "not-connected");
        var shell = await Shell();
        await ShellWait.Until(shell, () => !shell.Elsewhere.IsLoading, Token);

        shell.Elsewhere.Open(ElsewherePart.Linear);

        Assert.Equal(PageId.Settings, shell.CurrentPage);
    }, Token);

    [Fact]
    public Task The_needs_you_part_moves_focus_to_the_first_Needs_you_row() => UiThread.Run(async () =>
    {
        _server.NeedsYouView = RouteAnswer.Api("needs-you", "fixture");
        var shell = await Shell();
        await ShellWait.Until(shell, () => shell.NeedsYou.Rows.Count > 0, Token);
        shell.GoTo(PageId.Agents);
        var asked = new List<HomeFocusTarget>();
        shell.HomeFocusRequested += (_, e) => asked.Add(e.Target);

        shell.Elsewhere.Open(ElsewherePart.NeedsYou);

        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.Equal(shell.NeedsYou.Rows[0].Id, shell.NeedsYou.SelectedId);
        Assert.Equal([HomeFocusTarget.NeedsYou], asked);
    }, Token);

    [Fact]
    public Task Retry_on_reviews_reads_the_list_again() => UiThread.Run(async () =>
    {
        _server.ReviewsAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.InternalServerError, "SND-X-500", "down");
        var shell = await Shell();
        await ShellWait.Until(shell, () => shell.Elsewhere.Parts.Any(p => p.Part == ElsewherePart.Reviews && p.CanRetry), Token);
        var reads = _server.ReviewsCalls;
        _server.ReviewsAnswer = RouteAnswer.Api("reviews", "some");

        await shell.Elsewhere.Retry(ElsewherePart.Reviews);
        await ShellWait.Until(shell, () => shell.Elsewhere.Text.StartsWith("2 PRs", StringComparison.Ordinal), Token);

        Assert.True(_server.ReviewsCalls > reads);
    }, Token);

    [Fact]
    public Task G_T_shows_Home_and_asks_for_focus_on_the_Todo_panel() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Agents);
        var asked = new List<HomeFocusTarget>();
        shell.HomeFocusRequested += (_, e) => asked.Add(e.Target);

        _ = shell.PressKey("G");
        Assert.True(shell.PressKey("T"));

        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.Equal([HomeFocusTarget.Todo], asked);
    }, Token);

    [Fact]
    public Task The_palette_lists_Todo_with_G_T_and_running_it_does_what_the_chord_does() => UiThread.Run(async () =>
    {
        var shell = await Shell();
        shell.GoTo(PageId.Agents);
        var asked = new List<HomeFocusTarget>();
        shell.HomeFocusRequested += (_, e) => asked.Add(e.Target);
        shell.Palette.Open();

        var todo = Assert.Single(shell.Palette.Sections[1].Entries, e => e.Label == "Todo");
        await shell.Palette.Run(todo, Token);

        Assert.Equal("G T", todo.Key);
        Assert.Equal(PageId.Home, shell.CurrentPage);
        Assert.Equal([HomeFocusTarget.Todo], asked);
        Assert.False(shell.Palette.IsOpen);
    }, Token);

    [Fact]
    public Task The_shortcuts_sheet_names_G_T() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        shell.Shortcuts.Open();

        Assert.Contains(shell.Shortcuts.Rows, r => r.Keys.StartsWith("G then ", StringComparison.Ordinal) && r.Keys.EndsWith(" T", StringComparison.Ordinal) && r.Action.Contains("T Todo", StringComparison.Ordinal));
    }, Token);

    [Fact]
    public Task The_tray_tooltip_counts_overdue_todos() => UiThread.Run(async () =>
    {
        var shell = await Shell();

        await ShellWait.Until(shell, () => shell.Todo.OverdueCount > 0, Token);
        await ShellWait.Until(shell, () => shell.Attention.Tray.Tooltip.Contains("overdue todo", StringComparison.Ordinal), Token);

        Assert.Contains(Labels.TrayTodos(shell.Todo.OverdueCount, 0), shell.Attention.Tray.Tooltip, StringComparison.Ordinal);
    }, Token);
}
