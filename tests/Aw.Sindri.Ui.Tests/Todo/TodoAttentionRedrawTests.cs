using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Todo;
using static Aw.Sindri.Ui.Tests.Todo.AttentionFixture;
using static Aw.Sindri.Ui.Tests.Todo.TodoFixture;

namespace Aw.Sindri.Ui.Tests.Todo;

/// <summary>What the attention rows do when a read or a refused pick redraws them (todo spec §3.3, D13): focus is asked for again, the quiet window sees any change, a failed note read keeps the form, an older Sindri is named.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoAttentionRedrawTests
{
    private readonly AttentionFixture _a = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private FakeSindriApi Server => _a.F.Server;

    private static System.Text.Json.Nodes.JsonNode[] Todos(params System.Text.Json.Nodes.JsonNode[] first) => [.. first, .. Six()];

    private async Task<TodoPanelViewModel> OneConflict()
    {
        _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1", "reply to Sam", "reply to Sam today")));
        return await _a.Read(Token);
    }

    private void ServeMovedLine() => _a.Serve(TodoNoteSample.WithConflicts("td_c1"), Todos(TextOnly("td_c1", "reply to Sam", "reply to Sam by noon")));

    [Fact]
    public async Task A_pick_refused_because_the_line_moved_asks_for_focus_on_the_open_form_again()
    {
        var panel = await OneConflict();
        _ = panel.OpenAttention();
        var asked = _a.ButtonsAsked;
        ServeMovedLine();
        Server.TodoPickAnswer = RouteAnswer.Problem(HttpStatusCode.Conflict, "SND-TODO-409", "The note's line changed since.");

        await panel.Pick(2);

        Assert.True(_a.ButtonsAsked > asked);
        Assert.True(panel.Attention!.IsOpen);
    }

    [Fact]
    public async Task A_sync_that_redraws_the_open_form_asks_for_focus_again_and_one_that_changes_nothing_does_not()
    {
        var panel = await OneConflict();
        _ = panel.OpenAttention();
        var asked = _a.ButtonsAsked;

        await panel.Refresh(Url, Token);
        Assert.Equal(asked, _a.ButtonsAsked);

        ServeMovedLine();
        await panel.Refresh(Url, Token);
        Assert.True(_a.ButtonsAsked > asked);
    }

    [Fact]
    public async Task A_keep_answer_with_no_todo_reads_the_list_again()
    {
        var panel = await OneConflict();
        var reads = Server.TodosCalls;

        await panel.Pick(1);

        Assert.True(Server.TodosCalls > reads);
    }

    [Fact]
    public async Task A_sync_that_changes_the_text_of_the_open_conflict_starts_the_quiet_window()
    {
        var panel = await OneConflict();
        ServeMovedLine();
        await panel.Refresh(Url, Token);

        await panel.Pick(1);
        Assert.Empty(Server.TodoPicks);

        _a.F.Clock.Advance(TimeSpan.FromMilliseconds(500));
        await panel.Pick(1);
        _ = Assert.Single(Server.TodoPicks);
    }

    [Fact]
    public async Task A_sync_that_changes_the_count_of_a_hold_starts_the_quiet_window()
    {
        _a.Serve(TodoNoteSample.WithHold(4, "sha256:3f1c"), Todos());
        var panel = await _a.Read(Token);
        _a.Serve(TodoNoteSample.WithHold(5, "sha256:4444"), Todos());
        await panel.Refresh(Url, Token);

        await panel.Pick(1);

        Assert.Empty(Server.TodoPicks);
    }

    [Fact]
    public async Task A_note_read_that_fails_with_a_server_error_keeps_the_open_form()
    {
        var panel = await OneConflict();
        _ = panel.OpenAttention();
        Server.TodoNoteAnswer = new RouteAnswer(HttpStatusCode.InternalServerError, string.Empty);

        await panel.Refresh(Url, Token);

        Assert.True(panel.Attention is { IsOpen: true });
    }

    [Fact]
    public async Task A_bare_404_on_the_keep_route_names_the_older_Sindri()
    {
        var panel = await OneConflict();
        Server.TodoPickAnswer = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);

        await panel.Pick(1);

        Assert.Contains("older", panel.Attention!.Notice, StringComparison.Ordinal);
        Assert.DoesNotContain("404", panel.Attention.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bare_404_on_a_hold_route_names_the_older_Sindri()
    {
        _a.Serve(TodoNoteSample.WithHold(4, "sha256:3f1c"), Todos());
        var panel = await _a.Read(Token);
        Server.TodoPickAnswer = new RouteAnswer(HttpStatusCode.NotFound, string.Empty);

        await panel.Pick(2);

        Assert.Contains("older", panel.Attention!.Notice, StringComparison.Ordinal);
    }
}
