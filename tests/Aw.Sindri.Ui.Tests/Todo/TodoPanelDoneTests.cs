using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The panel's Show done toggle (todo spec §3.1): todos done in the last 7 days, removed ones under "Removed" for 24 hours, and the empty sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoPanelDoneTests
{
    private readonly TodoFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static bool IsDoneGroup(TodoGroupViewModel group) => string.Equals(group.Heading, "Done", StringComparison.Ordinal) || string.Equals(group.Heading, "Removed", StringComparison.Ordinal);

    private static string[] Texts(IEnumerable<TodoRowViewModel> rows) => [.. rows.Select(r => r.Text)];

    [Fact]
    public async Task The_done_group_is_off_until_show_done_is_pressed()
    {
        var panel = await _f.Expanded(Token);

        Assert.False(panel.ShowsDone);
        Assert.DoesNotContain(panel.Groups, IsDoneGroup);
    }

    [Fact]
    public async Task Show_done_adds_a_done_group_and_a_removed_group_after_the_open_ones()
    {
        var panel = await _f.Expanded(Token);

        panel.ToggleDone();

        Assert.True(panel.ShowsDone);
        Assert.Equal(["Overdue", "Today", "Upcoming", "No date", "Done", "Removed"], panel.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["renew the cert"], Texts(panel.Groups[4].Rows), StringComparer.Ordinal);
        Assert.Equal(["book the offsite room"], Texts(panel.Groups[5].Rows), StringComparer.Ordinal);
        Assert.Equal(string.Empty, panel.DoneEmptyText);
    }

    [Fact]
    public async Task The_toggle_reads_show_done_then_hide_done_and_is_named_with_the_count()
    {
        var panel = await _f.Expanded(Token);
        Assert.Equal(("Show done", "Show done todos, 2"), (panel.DoneToggleText, panel.DoneToggleName));

        panel.ToggleDone();
        Assert.Equal(("Hide done", "Show done todos, 2"), (panel.DoneToggleText, panel.DoneToggleName));

        panel.ToggleDone();
        Assert.False(panel.ShowsDone);
    }

    [Fact]
    public async Task A_todo_done_eight_days_ago_is_not_listed_and_one_done_seven_days_ago_is()
    {
        var old = Of(0);
        old["id"] = "td_old";
        old["text"] = "too old";
        old["doneAt"] = "2025-12-24T00:00:00.0000000Z";
        var edge = Of(0);
        edge["id"] = "td_edge";
        edge["text"] = "just in";
        edge["doneAt"] = "2025-12-25T00:00:00.0000000Z";
        _f.ServeTodos(Open("td_a", "open one"), old, edge);
        var panel = await _f.Expanded(Token);

        panel.ToggleDone();

        Assert.Equal(["just in"], Texts(panel.Groups.Single(g => g.Heading == "Done").Rows), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_todo_removed_over_a_day_ago_is_not_listed()
    {
        var gone = Of(6);
        gone["removedAt"] = "2025-12-30T23:59:00.0000000Z";
        var fresh = Of(6);
        fresh["id"] = "td_fresh";
        fresh["text"] = "removed an hour ago";
        fresh["removedAt"] = "2025-12-31T23:00:00.0000000Z";
        _f.ServeTodos(Open("td_a", "open one"), gone, fresh);
        var panel = await _f.Expanded(Token);

        panel.ToggleDone();

        Assert.Equal(["removed an hour ago"], Texts(panel.Groups.Single(g => g.Heading == "Removed").Rows), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_todo_both_done_and_removed_is_listed_once_under_removed()
    {
        var both = Of(0);
        both["removed"] = true;
        both["removedAt"] = "2025-12-31T20:00:00.0000000Z";
        _f.ServeTodos(Open("td_a", "open one"), both);
        var panel = await _f.Expanded(Token);

        panel.ToggleDone();

        Assert.Equal(["No date", "Removed"], panel.Groups.Select(g => g.Heading), StringComparer.Ordinal);
    }

    [Fact]
    public async Task With_nothing_done_and_nothing_removed_the_panel_says_nothing_was_done()
    {
        _f.ServeTodos(Open("td_a", "open one"));
        var panel = await _f.Expanded(Token);

        panel.ToggleDone();

        Assert.True(panel.ShowsDone);
        Assert.Equal("Nothing done in the last 7 days", panel.DoneEmptyText);
        Assert.DoesNotContain(panel.Groups, IsDoneGroup);
        Assert.Equal("Show done todos, 0", panel.DoneToggleName);
    }

    [Fact]
    public async Task Folding_hides_the_done_group_again()
    {
        var panel = await _f.Expanded(Token);
        panel.ToggleDone();

        panel.Fold();
        panel.Expand();

        Assert.False(panel.ShowsDone);
        Assert.DoesNotContain(panel.Groups, IsDoneGroup);
        Assert.Equal(string.Empty, panel.DoneEmptyText);
    }

    [Fact]
    public async Task Show_done_does_nothing_while_the_panel_is_collapsed()
    {
        var panel = await _f.Read(Token);

        panel.ToggleDone();

        Assert.False(panel.ShowsDone);
    }

    [Fact]
    public async Task A_re_read_keeps_the_done_group_open_and_follows_the_new_answer()
    {
        var panel = await _f.Expanded(Token);
        panel.ToggleDone();
        var again = Of(0);
        again["id"] = "td_new_done";
        again["text"] = "ticked just now";
        again["doneAt"] = "2025-12-31T23:59:00.0000000Z";
        _f.ServeTodos(Open("td_a", "open one"), again);

        await panel.Refresh(Url, Token);

        Assert.True(panel.ShowsDone);
        Assert.Equal(["ticked just now"], Texts(panel.Groups.Single(g => g.Heading == "Done").Rows), StringComparer.Ordinal);
    }
}
