using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.AttentionFixture;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>The attention rows' picks (todo spec §3.3, §3.4, D11): the digits, the calls they make, the toast with its Undo, and what each refusal does to the row.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoAttentionPicksTests
{
    private readonly AttentionFixture _a = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FakeSindriApi Server => _a.F.Server;

    private static JsonNode[] Todos(params JsonNode[] first) => [.. first, .. Six()];

    private async Task<TodoPanelViewModel> OneConflict()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1", "reply to Sam", "reply to Sam today")));
        return await _a.Read(Token);
    }

    private async Task<TodoPanelViewModel> OneHold(int count = 4)
    {
        _a.Serve(TodoNoteSample.WithHold(count, "sha256:3f1c"), Todos());
        return await _a.Read(Token);
    }

    [Fact]
    public async Task One_keeps_Sindri_s_version_and_two_keeps_Obsidian_s()
    {
        var panel = await OneConflict();

        await panel.Pick(1);
        await panel.Pick(2);

        Assert.Equal([(TodoRoutes.Keep("td_c1"), new TodoKeepBody("sindri")), (TodoRoutes.Keep("td_c1"), new TodoKeepBody("obsidian"))], Server.TodoPicks.Select(p => (p.Route, (TodoKeepBody)p.Body!)));
    }

    [Fact]
    public async Task A_pick_opens_the_toast_with_its_words_and_clears_the_row()
    {
        var panel = await OneConflict();

        await panel.Pick(2);

        var toast = Assert.Single(_a.Picked);
        Assert.Equal("Kept Obsidian's version.", toast.Text);
        Assert.Null(panel.Attention);   // the row goes
        Assert.Contains(panel.Rows, r => r.Id == "td_c1");   // the todo shows the kept version as a plain row
    }

    [Fact]
    public async Task A_pick_that_removes_or_keeps_a_removal_says_so()
    {
        var removedHere = Conflicted("td_c1", "reply to Sam", AttentionFixture.Side(text: null, due: null, removed: true), AttentionFixture.Side("edited"));
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(removedHere));
        var panel = await _a.Read(Token);

        await panel.Pick(1);

        Assert.Equal("Kept it removed.", Assert.Single(_a.Picked).Text);
    }

    [Fact]
    public async Task Undo_sends_the_undo_route_and_the_conflict_is_back_after_the_next_read()
    {
        var panel = await OneConflict();
        Server.OnTodoPicked = (route, _) =>
        {
            if (route.EndsWith("/keep", StringComparison.Ordinal))
            {
                _a.F.ServeTodos(Todos(Open("td_c1", "reply to Sam today", "2026-01-03")));   // the kept version, no conflict
                Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSample.Quiet);
            }
            else
            {
                _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1", "reply to Sam", "reply to Sam today")));   // the conflict reopened
            }
        };
        await panel.Pick(2);

        var failure = await _a.Picked[0].Undo();

        Assert.Null(failure);
        Assert.Equal(TodoRoutes.KeepUndo("td_c1"), Server.TodoPicks[^1].Route);
        Assert.Null(Server.TodoPicks[^1].Body);
        Assert.Equal("td_c1", panel.Attention?.Id);
        Assert.False(panel.Attention!.IsOpen);
    }

    [Fact]
    public async Task Undo_after_the_window_says_there_is_nothing_to_undo()
    {
        var panel = await OneConflict();
        await panel.Pick(1);
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-005", "There's nothing to undo.");

        var failure = await _a.Picked[0].Undo();

        Assert.Equal("there's nothing to undo", failure);
    }

    [Fact]
    public async Task Undo_refused_because_it_changed_meanwhile_reads_again_and_says_so()
    {
        var panel = await OneConflict();
        await panel.Pick(1);
        var reads = Server.TodosCalls;
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "changed");

        var failure = await _a.Picked[0].Undo();

        Assert.Equal("it changed since. Here's the latest", failure);
        Assert.True(Server.TodosCalls > reads);
        _ = panel;
    }

    [Fact]
    public async Task Keep_Obsidian_s_refused_with_409_reads_again_and_the_row_says_the_line_moved()
    {
        var panel = await OneConflict();
        var reads = Server.TodosCalls;
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note's line changed since.");

        await panel.Pick(2);

        Assert.Empty(_a.Picked);
        Assert.True(Server.TodosCalls > reads);
        Assert.Equal("The note's line changed since. Here's the latest", panel.Attention!.Notice);
    }

    [Fact]
    public async Task Another_refusal_keeps_the_row_with_the_reason()
    {
        var panel = await OneConflict();
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-TODO-404", "No todo td_c1.");

        await panel.Pick(1);

        Assert.Empty(_a.Picked);
        Assert.Equal("Couldn't save: No todo td_c1.", panel.Attention!.Notice);
    }

    [Fact]
    public async Task X_Space_and_Delete_on_a_conflict_row_send_nothing_and_say_pick_a_version_first()
    {
        var panel = await OneConflict();

        Assert.True(panel.NoPick());

        Assert.Equal("Pick a version first", panel.Attention!.Notice);
        Assert.Empty(Server.TodoPicks);
        Assert.Empty(Server.TodoWrites);
    }

    [Fact]
    public async Task A_hold_pick_one_puts_the_lines_back_with_the_note_hash_and_two_applies()
    {
        var panel = await OneHold();

        await panel.Pick(1);
        await panel.Pick(2);

        Assert.Equal([TodoRoutes.HoldRestore, TodoRoutes.HoldApply], Server.TodoPicks.Select(p => p.Route), StringComparer.Ordinal);
        Assert.All(Server.TodoPicks, p => Assert.Equal(new TodoHoldBody("sha256:3f1c"), p.Body));
    }

    [Fact]
    public async Task A_hold_pick_shows_its_toast_and_the_row_goes()
    {
        var panel = await OneHold(14);

        await panel.Pick(2);
        Assert.Equal("Applied 14 changes.", _a.Picked[0].Text);
        Assert.Null(panel.Attention);
    }

    [Fact]
    public async Task Put_them_back_says_putting_14_back_in_the_note()
    {
        var panel = await OneHold(14);

        await panel.Pick(1);

        Assert.Equal("Putting 14 back in the note.", Assert.Single(_a.Picked).Text);
    }

    [Fact]
    public async Task A_hold_Undo_sends_the_hold_undo_route_and_the_hold_comes_back_on_the_next_read()
    {
        var panel = await OneHold();
        Server.OnTodoPicked = (route, _) => Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, route == TodoRoutes.HoldUndo ? TodoNoteSample.WithHold(4, "sha256:3f1c") : TodoNoteSample.Quiet);
        await panel.Pick(1);

        var failure = await _a.Picked[0].Undo();

        Assert.Null(failure);
        Assert.Equal(TodoRoutes.HoldUndo, Server.TodoPicks[^1].Route);
        Assert.Equal(AttentionKind.Hold, panel.Attention?.Kind);
    }

    [Fact]
    public async Task A_hold_pick_refused_because_the_note_changed_reads_again_and_says_the_new_count()
    {
        var panel = await OneHold(14);
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note changed again: now 9. Check and pick again.");
        Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSample.WithHold(9, "sha256:9999"));

        await panel.Pick(2);

        Assert.Empty(_a.Picked);
        Assert.Equal(("The note changed 9 todos.", "The note changed again: now 9. Check and pick again."), (panel.Attention!.Title, panel.Attention.Notice));
        Assert.Equal("sha256:9999", panel.Attention.NoteHash);
    }

    [Fact]
    public async Task A_hold_pick_refused_with_nothing_held_drops_the_row()
    {
        var panel = await OneHold();
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-005", "The note has no changes waiting.");
        Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.OK, TodoNoteSample.Quiet);

        await panel.Pick(1);

        Assert.Empty(_a.Picked);
        Assert.Null(panel.Attention);
    }

    [Fact]
    public async Task A_digit_with_no_attention_row_does_nothing()
    {
        _a.Serve(TodoNoteSample.Quiet, Todos());
        var panel = await _a.Read(Token);

        await panel.Pick(1);

        Assert.Empty(Server.TodoPicks);
    }

    [Fact]
    public async Task A_digit_within_half_a_second_of_a_row_the_owner_did_not_cause_is_ignored()
    {
        _a.Serve(TodoNoteSample.Quiet, Todos());
        var panel = await _a.Read(Token);
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1")));
        await panel.Refresh(TodoFixture.Url, Token);   // a sync brought a conflict in

        await panel.Pick(1);
        Assert.Empty(Server.TodoPicks);

        _a.F.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await panel.Pick(1);
        _ = Assert.Single(Server.TodoPicks);
    }

    [Fact]
    public async Task A_row_that_was_there_from_the_first_read_takes_a_digit_at_once()
    {
        var panel = await OneConflict();

        await panel.Pick(1);

        _ = Assert.Single(Server.TodoPicks);
    }
}
