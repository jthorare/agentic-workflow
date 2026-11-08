using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>Check off, reopen, remove and restore from the keys (todo spec §3.3, §3.4): the row leaves at once, focus moves on, a failure rolls back, the toast's Undo brings the todo back.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoRowActionsTests
{
    private readonly TodoFixture _f = new();
    private readonly List<TodoActedEventArgs> _acted = [];
    private readonly List<string?> _focus = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Ids(IEnumerable<TodoRowViewModel> rows) => [.. rows.Select(r => r.Id)];

    // Three open todos, in the order the panel lists them: a (due today), then b and c (no date).
    private async Task<TodoPanelViewModel> Three()
    {
        _f.ServeTodos(Open("td_a", "first", "2026-01-01"), Open("td_b", "second"), Open("td_c", "third"));
        var panel = await _f.Read(Token);
        panel.Acted += (_, e) => _acted.Add(e);
        panel.FocusRequested += (_, e) => _focus.Add(e.Id);
        return panel;
    }

    [Fact]
    public async Task X_on_an_open_row_removes_it_from_the_panel_before_Sindri_answers()
    {
        var panel = await Three();
        _f.Server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = panel.Toggle("td_a");

        Assert.Equal(["td_b", "td_c"], Ids(panel.Rows));
        Assert.False(pending.IsCompleted);
        _f.Server.CommandGate.SetResult();
        await pending;
    }

    [Fact]
    public async Task X_sends_done_at_the_version_the_row_showed()
    {
        var panel = await Three();
        var version = panel.Rows[0].Version;

        await panel.Toggle("td_a");

        var sent = Assert.Single(_f.Server.TodoWrites);
        Assert.Equal(TodoRoutes.Done("td_a"), sent.Route);
        Assert.Equal(new TodoVersion(version), sent.Body);
    }

    [Fact]
    public async Task Focus_moves_to_the_next_row_or_to_the_previous_one_at_the_end()
    {
        var panel = await Three();

        await panel.Toggle("td_a");
        await panel.Toggle("td_c");

        Assert.Equal(["td_b", "td_b"], _focus);
    }

    [Fact]
    public async Task Focus_goes_to_the_panel_when_the_last_row_leaves()
    {
        _f.ServeTodos(Open("td_only", "alone"));
        var panel = await _f.Read(Token);
        panel.FocusRequested += (_, e) => _focus.Add(e.Id);

        await panel.Toggle("td_only");

        Assert.Null(Assert.Single(_focus));
    }

    [Fact]
    public async Task A_done_check_off_asks_for_a_toast_that_says_Done_and_a_delete_one_that_says_Removed()
    {
        var panel = await Three();

        await panel.Toggle("td_a");
        await panel.Remove("td_b");

        Assert.Equal(["Done", "Removed"], _acted.Select(a => a.Text), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Delete_sends_remove_and_the_row_leaves()
    {
        var panel = await Three();

        await panel.Remove("td_b");

        Assert.Equal(TodoRoutes.Remove("td_b"), Assert.Single(_f.Server.TodoWrites).Route);
        Assert.Equal(["td_a", "td_c"], Ids(panel.Rows));
    }

    [Fact]
    public async Task A_failed_write_brings_the_row_back_with_the_reason_and_focus_returns_to_it()
    {
        var panel = await Three();
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");

        await panel.Toggle("td_b");

        Assert.Equal(["td_a", "td_b", "td_c"], Ids(panel.Rows));
        Assert.Equal("Couldn't save: Try again.", panel.Rows.Single(r => r.Id == "td_b").Notice);
        Assert.Equal(["td_c", "td_b"], _focus);   // on to the next row first, back to the row on failure
        Assert.Empty(_acted);
    }

    [Fact]
    public async Task Undo_of_a_check_off_reopens_the_todo_and_puts_focus_on_it()
    {
        var panel = await Three();
        var version = panel.Rows[0].Version;
        await panel.Toggle("td_a");

        var failure = await _acted.Single().Undo();

        Assert.Null(failure);
        var reopen = _f.Server.TodoWrites[1];
        Assert.Equal(TodoRoutes.Reopen("td_a"), reopen.Route);
        Assert.Equal(new TodoVersion(version + 1), reopen.Body);   // the version Sindri answered the check-off with
        Assert.Equal(["td_a", "td_b", "td_c"], Ids(panel.Rows));
        Assert.Equal("td_a", _focus[^1]);
    }

    [Fact]
    public async Task Undo_of_a_delete_restores_the_todo()
    {
        var panel = await Three();
        await panel.Remove("td_b");

        var failure = await _acted.Single().Undo();

        Assert.Null(failure);
        Assert.Equal(TodoRoutes.Restore("td_b"), _f.Server.TodoWrites[1].Route);
        Assert.Equal(["td_a", "td_b", "td_c"], Ids(panel.Rows));
    }

    [Fact]
    public async Task An_Undo_refused_with_409_says_it_changed_since_and_reads_the_list_again()
    {
        var panel = await Three();
        await panel.Toggle("td_a");
        var reads = _f.Server.TodosCalls;
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "Changed since.");

        var failure = await _acted.Single().Undo();

        Assert.Equal("it changed since. Here's the latest", failure);
        Assert.True(_f.Server.TodosCalls > reads);
    }

    [Fact]
    public async Task X_on_a_done_row_reopens_it_and_on_a_removed_row_restores_it()
    {
        var done = Of(0);
        done["id"] = "td_done";
        done["text"] = "finished";
        done["doneAt"] = "2025-12-30T00:00:00.0000000Z";
        var removed = Of(0);
        removed["id"] = "td_gone";
        removed["text"] = "dropped";
        removed["doneAt"] = null;
        removed["removed"] = true;
        removed["removedAt"] = "2025-12-31T12:00:00.0000000Z";
        _f.ServeTodos(Open("td_a", "first"), done, removed);
        var panel = await _f.Expanded(Token);
        panel.ToggleDone();

        await panel.Toggle("td_done");
        await panel.Toggle("td_gone");

        Assert.Equal([TodoRoutes.Reopen("td_done"), TodoRoutes.Restore("td_gone")], _f.Server.TodoWrites.Select(w => w.Route), StringComparer.Ordinal);
        Assert.Equal(["td_a", "td_done", "td_gone"], Ids(panel.Groups.SelectMany(g => g.Rows)).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.DoesNotContain(panel.Groups, g => string.Equals(g.Heading, "Done", StringComparison.Ordinal) || string.Equals(g.Heading, "Removed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_failed_write_then_a_saved_one_leaves_no_notice_on_the_row()
    {
        _f.ServeTodos(Open("td_a", "first"), Open("td_b", "second"));
        var panel = await _f.Expanded(Token);
        panel.ToggleDone();
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");
        await panel.Toggle("td_b");
        _f.Server.TodoWriteAnswer = null;

        await panel.Toggle("td_b");

        var row = panel.Groups.SelectMany(g => g.Rows).Single(r => r.Id == "td_b");
        Assert.True(row.IsDone);
        Assert.Equal(string.Empty, row.Notice);
    }

    [Fact]
    public async Task A_read_that_brings_a_newer_version_clears_the_notice()
    {
        var panel = await Three();
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");
        await panel.Toggle("td_b");
        var edited = Open("td_b", "second, edited elsewhere");
        edited["version"] = panel.Rows.Single(r => r.Id == "td_b").Version + 1;
        _f.ServeTodos(Open("td_a", "first", "2026-01-01"), edited, Open("td_c", "third"));

        await panel.Refresh(Url, Token);

        Assert.Equal(string.Empty, panel.Rows.Single(r => r.Id == "td_b").Notice);
    }

    [Fact]
    public async Task A_read_asked_for_before_the_write_answered_does_not_bring_the_old_state_back()
    {
        var panel = await Three();
        _f.Server.TodosGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var old = panel.Refresh(Url, Token);   // answers with td_a still open

        await panel.Toggle("td_a");
        _f.Server.TodosGate.SetResult();
        await old;

        Assert.Equal(["td_b", "td_c"], Ids(panel.Rows));
    }
}
