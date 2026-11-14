using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The Todo panel's groups, collapsed and expanded states and count line (todo spec §3.1, §3.4), with the golden's dates falling in each group on 2026-01-01.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoPanelTests
{
    private readonly TodoFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Texts(IEnumerable<TodoRowViewModel> rows) => [.. rows.Select(r => r.Text)];

    [Fact]
    public async Task The_panel_is_loading_until_its_first_answer()
    {
        var panel = _f.Panel();
        Assert.True(panel.IsLoading);

        await panel.Refresh(Url, Token);

        Assert.False(panel.IsLoading);
    }

    [Fact]
    public async Task A_read_asks_for_every_state_because_the_panel_works_out_done_and_removed_itself()
    {
        _ = await _f.Read(Token);

        Assert.Equal(["all"], _f.Server.TodosStates, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_expanded_panel_groups_the_open_todos_overdue_first()
    {
        var panel = await _f.Expanded(Token);

        Assert.Equal(["Overdue", "Today", "Upcoming", "No date"], panel.Groups.Select(g => g.Heading), StringComparer.Ordinal);
        Assert.Equal(["rotate the staging key"], Texts(panel.Groups[0].Rows), StringComparer.Ordinal);
        Assert.Equal(["check the flaky sync test"], Texts(panel.Groups[1].Rows), StringComparer.Ordinal);
        Assert.Equal(["reply to Sam about the offsite", "answer the review comments on acme/web#40"], Texts(panel.Groups[2].Rows), StringComparer.Ordinal);
        Assert.Equal(["call the dentist"], Texts(panel.Groups[3].Rows), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_group_with_no_todos_is_not_drawn()
    {
        _f.ServeTodos(Open("td_a", "later", "2026-02-01"), Open("td_b", "someday"));

        var panel = await _f.Expanded(Token);

        Assert.Equal(["Upcoming", "No date"], panel.Groups.Select(g => g.Heading), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Overdue_and_upcoming_list_the_nearest_day_first_and_keep_the_server_order_on_a_tie()
    {
        _f.ServeTodos(
            Open("td_1", "older", "2025-12-20"), Open("td_2", "oldest", "2025-12-10"),
            Open("td_3", "next-b", "2026-01-05"), Open("td_4", "next-a", "2026-01-03"), Open("td_5", "next-b2", "2026-01-05"));

        var panel = await _f.Expanded(Token);

        Assert.Equal(["oldest", "older"], Texts(panel.Groups[0].Rows), StringComparer.Ordinal);
        Assert.Equal(["next-a", "next-b", "next-b2"], Texts(panel.Groups[1].Rows), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Collapsed_the_panel_shows_the_open_todos_across_the_groups_up_to_five_with_no_show_all_at_five()
    {
        var panel = await _f.Read(Token);

        Assert.Equal(
            ["rotate the staging key", "check the flaky sync test", "reply to Sam about the offsite", "answer the review comments on acme/web#40", "call the dentist"],
            Texts(panel.Rows), StringComparer.Ordinal);
        Assert.False(panel.IsExpanded);
        Assert.False(panel.ShowsShowAll);
    }

    [Fact]
    public async Task More_than_five_open_todos_collapse_to_the_first_five_and_offer_show_all()
    {
        var more = Enumerable.Range(1, 11).Select(i => Open(string.Create(CultureInfo.InvariantCulture, $"td_{i}"), string.Create(CultureInfo.InvariantCulture, $"todo {i}"), i == 1 ? "2025-12-30" : null)).ToArray();
        _f.ServeTodos(more);

        var panel = await _f.Read(Token);

        Assert.Equal(["todo 1", "todo 2", "todo 3", "todo 4", "todo 5"], Texts(panel.Rows), StringComparer.Ordinal);
        Assert.Equal((true, "Show all 11"), (panel.ShowsShowAll, panel.ShowAllText));

        panel.Expand();

        Assert.Equal((true, false), (panel.IsExpanded, panel.ShowsShowAll));
        Assert.Equal(11, panel.Groups.Sum(g => g.Rows.Count));
    }

    [Fact]
    public async Task Folding_goes_back_to_the_collapsed_rows()
    {
        var panel = await _f.Expanded(Token);

        panel.Fold();

        Assert.False(panel.IsExpanded);
    }

    [Fact]
    public async Task The_count_line_counts_the_open_todos_and_the_overdue_ones()
    {
        Assert.Equal("5 open · 1 overdue", (await _f.Read(Token)).CountLine);

        _f.ServeTodos(Open("td_a", "later", "2026-02-01"));
        Assert.Equal("1 open", (await _f.Read(Token)).CountLine);
    }

    [Fact]
    public async Task An_open_conflict_adds_to_sort_out_to_the_count_line()
    {
        var conflicted = Of(1);
        conflicted["conflict"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"sindri":{"text":"reply to Sam","due":"2026-01-02","done":false},"obsidian":{"text":"reply","due":null,"done":false},"noteHash":"sha256:ab"}
            """);
        _f.ServeTodos(conflicted, Of(2));

        var panel = await _f.Read(Token);

        Assert.Equal("2 open · 1 overdue · 1 to sort out", panel.CountLine);
        Assert.Equal("td_01jc5a8xq0n4w7ms2t9r3vbk6e", panel.Attention?.Id);   // the conflict replaces that todo's row (spec §3.1)
        Assert.DoesNotContain(panel.Rows, r => string.Equals(r.Id, "td_01jc5a8xq0n4w7ms2t9r3vbk6e", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_conflict_on_a_todo_Sindri_removed_still_counts_to_sort_out()
    {
        var removed = Of(6);
        removed["conflict"] = System.Text.Json.Nodes.JsonNode.Parse("""
            {"sindri":{"text":null,"due":null,"done":false,"removed":true},"obsidian":{"text":"old task, edited","due":null,"done":false},"noteHash":"sha256:cd"}
            """);
        _f.ServeTodos(Of(1), removed);

        var panel = await _f.Read(Token);

        Assert.EndsWith("· 1 to sort out", panel.CountLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task With_nothing_open_the_panel_says_so()
    {
        _f.ServeTodos(Of(0), Of(6));

        var panel = await _f.Read(Token);

        Assert.True(panel.IsEmpty);
        Assert.Equal("Nothing on your list. Press N to add one.", panel.EmptyText);
        Assert.Equal("0 open", panel.CountLine);
        Assert.Empty(panel.Rows);
    }

    [Fact]
    public async Task An_empty_list_collapses_to_one_line_and_a_list_with_todos_keeps_its_five_slots()
    {
        _f.ServeTodos(Of(0), Of(6));
        var empty = await _f.Read(Token);

        Assert.Equal(1, empty.CollapsedSlots);
        Assert.Equal(5, _f.Panel().CollapsedSlots);   // before the first read the panel is loading: full height
    }

    [Fact]
    public async Task A_list_with_open_todos_is_not_empty()
    {
        var panel = await _f.Read(Token);

        Assert.False(panel.IsEmpty);
        Assert.Equal(string.Empty, panel.EmptyText);
    }

    [Fact]
    public async Task A_failed_read_ends_loading_and_says_why()
    {
        _f.Server.TodosAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views are behind.");
        var panel = _f.Panel();

        await panel.Refresh(Url, Token);

        Assert.False(panel.IsLoading);
        Assert.NotEmpty(panel.Error);
    }

    [Fact]
    public async Task A_good_read_after_a_failed_one_clears_the_line()
    {
        _f.Server.TodosAnswer = RouteAnswer.Problem(System.Net.HttpStatusCode.ServiceUnavailable, "SND-API-011", "The views are behind.");
        var panel = _f.Panel();
        await panel.Refresh(Url, Token);
        _f.Server.TodosAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, TodoSample.List);

        await panel.Refresh(Url, Token);

        Assert.Equal(string.Empty, panel.Error);
        Assert.Equal(5, panel.Rows.Count);
    }

    [Fact]
    public async Task A_re_read_keeps_the_row_objects_so_focus_stays_put()
    {
        var panel = await _f.Read(Token);
        var before = panel.Rows.ToList();

        await panel.Refresh(Url, Token);

        Assert.True(before.SequenceEqual(panel.Rows, ReferenceEqualityComparer.Instance));
    }
}
