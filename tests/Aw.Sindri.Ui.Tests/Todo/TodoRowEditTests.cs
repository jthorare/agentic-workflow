using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>Enter on a todo row edits it in place (todo spec §3.3, §3.4, D9): only what changed is sent at the version the edit opened on, Esc sends nothing, and each refusal says what to do.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoRowEditTests
{
    private readonly TodoFixture _f = new();
    private readonly List<string?> _focus = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Two open todos: a (due Friday 2026-01-02) and b (no date). The clock reads Thursday 2026-01-01.
    private async Task<TodoPanelViewModel> Two()
    {
        _f.ServeTodos(Open("td_a", "first", "2026-01-02"), Open("td_b", "second"));
        var panel = await _f.Read(Token);
        panel.FocusRequested += (_, e) => _focus.Add(e.Id);
        return panel;
    }

    private static TodoEditViewModel Edit(TodoPanelViewModel panel, string id)
    {
        Assert.True(panel.BeginEdit(id));
        return Assert.IsType<TodoEditViewModel>(panel.Editing);
    }

    [Fact]
    public async Task Enter_opens_an_edit_with_the_text_the_day_and_the_links_of_the_todo()
    {
        var panel = await Two();

        var edit = Edit(panel, "td_a");

        Assert.Equal(("first", "2026-01-02", string.Empty), (edit.Text, edit.Due, edit.Links));
        Assert.Same(edit, panel.Rows[0].Edit);
    }

    [Fact]
    public async Task Save_sends_only_the_text_when_only_the_text_changed_at_the_version_the_row_showed()
    {
        var panel = await Two();
        var version = panel.Rows[0].Version;
        Edit(panel, "td_a").Text = "first, again";

        await panel.SaveEdit();

        var sent = Assert.Single(_f.Server.TodoWrites);
        Assert.Equal(TodoRoutes.Change("td_a"), sent.Route);
        Assert.Equal(new TodoChange(version, Text: "first, again"), sent.Body);
    }

    [Fact]
    public async Task A_day_word_is_read_like_the_quick_add_boxes_and_an_empty_field_clears_the_day()
    {
        var panel = await Two();
        Edit(panel, "td_b").Due = "tomorrow";
        await panel.SaveEdit();
        Edit(panel, "td_a").Due = string.Empty;
        await panel.SaveEdit();

        Assert.Equal(["2026-01-02", string.Empty], _f.Server.TodoWrites.Select(w => ((TodoChange)w.Body).Due), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Links_are_refs_separated_by_commas_and_the_whole_list_is_sent()
    {
        var panel = await Two();
        Edit(panel, "td_b").Links = "ENG-12, acme/api#12 ,";

        await panel.SaveEdit();

        Assert.Equal(["ENG-12", "acme/api#12"], ((TodoChange)Assert.Single(_f.Server.TodoWrites).Body).Links);
    }

    [Fact]
    public async Task Nothing_changed_closes_the_edit_and_sends_nothing()
    {
        var panel = await Two();
        _ = Edit(panel, "td_a");

        await panel.SaveEdit();

        Assert.Empty(_f.Server.TodoWrites);
        Assert.Null(panel.Editing);
    }

    [Fact]
    public async Task A_saved_edit_shows_the_new_values_closes_and_keeps_focus_on_the_row()
    {
        var panel = await Two();
        Edit(panel, "td_a").Text = "first, again";

        await panel.SaveEdit();

        Assert.Null(panel.Editing);
        Assert.Null(panel.Rows[0].Edit);
        Assert.Equal("first, again", panel.Rows[0].Text);
        Assert.Equal("td_a", Assert.Single(_focus));
    }

    [Fact]
    public async Task Esc_cancels_puts_the_row_back_sends_nothing_and_keeps_focus_on_the_row()
    {
        var panel = await Two();
        Edit(panel, "td_a").Text = "typed";

        Assert.True(panel.CancelEdit());

        Assert.Empty(_f.Server.TodoWrites);
        Assert.Null(panel.Rows[0].Edit);
        Assert.Equal("first", panel.Rows[0].Text);
        Assert.Equal("td_a", Assert.Single(_focus));
        Assert.False(panel.CancelEdit());
    }

    [Fact]
    public async Task Saving_shows_on_the_row_only_after_300_ms()
    {
        var panel = await Two();
        var edit = Edit(panel, "td_a");
        edit.Text = "slow";
        _f.Server.CommandGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = panel.SaveEdit();
        _f.Clock.Advance(TimeSpan.FromMilliseconds(299));
        Assert.False(edit.IsSaving);
        _f.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(edit.IsSaving);

        _f.Server.CommandGate.SetResult();
        await pending;
        Assert.Null(panel.Editing);
    }

    [Fact]
    public async Task An_answer_inside_300_ms_never_shows_Saving()
    {
        var panel = await Two();
        var edit = Edit(panel, "td_a");
        edit.Text = "quick";

        await panel.SaveEdit();
        _f.Clock.Advance(TimeSpan.FromSeconds(1));

        Assert.False(edit.IsSaving);
    }

    [Fact]
    public async Task A_409_keeps_the_typed_text_reads_the_list_again_and_says_it_changed_somewhere_else()
    {
        var panel = await Two();
        var edit = Edit(panel, "td_a");
        edit.Text = "typed";
        var reads = _f.Server.TodosCalls;
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The todo changed.");

        await panel.SaveEdit();

        Assert.Same(edit, panel.Editing);
        Assert.Equal(("typed", "Changed somewhere else. Here's the latest"), (edit.Text, edit.Message));
        Assert.Equal(reads + 1, _f.Server.TodosCalls);
        Assert.False(edit.IsSaving);
    }

    [Fact]
    public async Task A_409_shows_the_latest_takes_it_into_untouched_fields_and_the_next_Enter_sends_only_the_owners_change()
    {
        var panel = await Two();
        var version = panel.Rows[0].Version;
        var edit = Edit(panel, "td_a");
        edit.Text = "typed";
        var latest = Open("td_a", "first, from the CLI", "2026-01-05");
        latest["version"] = version + 1;
        _f.ServeTodos(latest, Open("td_b", "second"));
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The todo changed.");

        await panel.SaveEdit();

        Assert.Equal("Now: first, from the CLI · 2026-01-05", edit.Latest);
        Assert.Equal(("typed", "2026-01-05"), (edit.Text, edit.Due));   // the owner's text stays; the day they didn't touch is the latest
        _f.Server.TodoWriteAnswer = null;

        await panel.SaveEdit();

        Assert.Equal(new TodoChange(version + 1, Text: "typed"), _f.Server.TodoWrites[^1].Body);
    }

    [Fact]
    public async Task A_423_closes_the_edit_says_to_pick_a_version_and_puts_focus_on_the_row()
    {
        var panel = await Two();
        Edit(panel, "td_a").Text = "typed";
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.Locked, "SND-TODO-423", "Conflict open.");

        await panel.SaveEdit();

        Assert.Null(panel.Editing);
        Assert.Equal("Changed in both places. Pick a version first", panel.Rows[0].Notice);
        Assert.Equal("td_a", _focus[^1]);
    }

    [Fact]
    public async Task Any_other_refusal_keeps_the_edit_open_with_the_reason()
    {
        var panel = await Two();
        var edit = Edit(panel, "td_a");
        edit.Text = "typed";
        _f.Server.TodoWriteAnswer = RouteAnswer.Problem(HttpStatusCode.InternalServerError, "SND-TODO-001", "Try again.");

        await panel.SaveEdit();

        Assert.Same(edit, panel.Editing);
        Assert.Equal(("typed", "Couldn't save: Try again."), (edit.Text, edit.Message));
    }

    [Fact]
    public async Task A_read_that_lands_mid_edit_changes_the_row_and_leaves_the_field_alone()
    {
        var panel = await Two();
        var edit = Edit(panel, "td_a");
        edit.Text = "typed so far";
        var moved = Open("td_a", "changed elsewhere", "2026-01-02");
        moved["version"] = panel.Rows[0].Version + 1;
        _f.ServeTodos(moved, Open("td_b", "second"));

        await panel.Refresh(Url, Token);

        Assert.Equal("changed elsewhere", panel.Rows[0].Text);
        Assert.Equal("typed so far", edit.Text);
        Assert.Same(edit, panel.Editing);
    }

    [Fact]
    public async Task An_empty_text_or_a_word_that_is_no_day_keeps_the_edit_open_and_sends_nothing()
    {
        var panel = await Two();
        var edit = Edit(panel, "td_a");
        edit.Text = "  ";
        await panel.SaveEdit();
        var empty = edit.Message;
        edit.Text = "first";
        edit.Due = "blorp";
        await panel.SaveEdit();

        Assert.Equal("Type a todo first", empty);
        Assert.Equal("Not a day. Try Fri, tomorrow or 11-10", edit.Message);
        Assert.Empty(_f.Server.TodoWrites);
    }

    [Fact]
    public async Task X_and_Delete_do_nothing_to_a_row_that_is_being_edited()
    {
        var panel = await Two();
        _ = Edit(panel, "td_a");

        await panel.Toggle("td_a");
        await panel.Remove("td_a");

        Assert.Empty(_f.Server.TodoWrites);
    }
}
