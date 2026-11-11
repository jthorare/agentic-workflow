using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using Aw.Sindri.Features.GetTodoNote;

namespace Aw.Sindri.Tests.Todos;

/// <summary>What GET /sindri/todos/note serves beyond the sync status (todo spec §4.4): the open hold, the picks still in their Undo window and the note lines that can't sync.</summary>
[Lane(TestLane.Unit)]
public sealed class TodoNoteReadsTests
{
    private const string Hash = "sha256:3f1c0a9e";
    private static readonly TodoId _first = TodoHarness.First;
    private static readonly TodoId _second = TodoHarness.Second;
    private readonly TodoHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Line(TodoId id, string text) => $"- [ ] {text} ^td-{id.Value[3..]}";

    private async Task<GetTodoNoteResponse> Read()
    {
        await _h.Lease();
        _h.NoteLocator.Place = new NotePlace("notes", "Todo.md", "/vault");
        var note = await _h.Note().Handle(new GetTodoNoteRequest(), Token);
        Assert.True(note.IsSuccess, note.IsError ? note.Error.ToString() : null);
        return note.Value;
    }

    [Fact]
    public async Task An_open_hold_is_served_with_its_hash_its_time_and_each_change_with_sindris_text()
    {
        using var cli = _h.Cli();
        await _h.Seed(_first, "reply to Sam");
        await _h.Seed(_second, "call the dentist");
        await _h.Record(EventTypes.TodoNoteHeld, new TodoNoteHeld([new TodoHeldChange(_first, "removed", NoteLine: null), new TodoHeldChange(_second, "edited", Line(_second, "call the dentist about the crown"))], Hash, "service:todo-note"), "todo.note.held:" + Hash);

        var note = await Read();

        var hold = Assert.IsType<TodoNoteHold>(note.Hold);
        Assert.Equal((Hash, 2), (hold.NoteHash, hold.OpenInBase));
        Assert.Equal(_h.Slice.Clock.GetUtcNow(), hold.HeldAt);
        Assert.Equal([(_first, "removed", "reply to Sam", null), (_second, "edited", "call the dentist", Line(_second, "call the dentist about the crown"))], hold.Changes.Select(c => (c.Id, c.Kind, c.Text, c.NoteLine)));
    }

    [Fact]
    public async Task The_hold_keeps_the_base_it_was_drawn_against_when_todos_are_added_later()
    {
        using var cli = _h.Cli();
        await _h.Seed(_first, "reply to Sam");
        await _h.Seed(_second, "call the dentist");
        await _h.Record(EventTypes.TodoNoteHeld, new TodoNoteHeld([new TodoHeldChange(_first, "removed", NoteLine: null)], Hash, "service:todo-note", OpenInBase: 2), "todo.note.held:" + Hash);
        await _h.Seed(TodoId.From("td_00000000000000000000000009"), "a third todo, added after the hold");

        var note = await Read();

        Assert.Equal(2, Assert.IsType<TodoNoteHold>(note.Hold).OpenInBase);
    }

    [Fact]
    public async Task A_hold_from_before_the_base_was_stored_falls_back_to_the_open_todos_now()
    {
        using var cli = _h.Cli();
        await _h.Seed(_first, "reply to Sam");
        await _h.Seed(_second, "call the dentist");
        await _h.Record(EventTypes.TodoNoteHeld, new TodoNoteHeld([new TodoHeldChange(_first, "removed", NoteLine: null)], Hash, "service:todo-note"), "todo.note.held:" + Hash);

        var note = await Read();

        Assert.Equal(2, Assert.IsType<TodoNoteHold>(note.Hold).OpenInBase);
    }

    [Fact]
    public async Task A_resolved_hold_is_not_served()
    {
        using var cli = _h.Cli();
        await _h.Record(EventTypes.TodoNoteHeld, new TodoNoteHeld([new TodoHeldChange(_first, "removed", NoteLine: null)], Hash, "service:todo-note"), "todo.note.held:" + Hash);
        await _h.Record(EventTypes.TodoNoteHeldResolved, new TodoNoteHeldResolved("restored", Hash, 1, "human:ui"), "todo.note.held.resolved:" + Hash);

        var note = await Read();

        Assert.Null(note.Hold);
    }

    [Fact]
    public async Task Picks_inside_their_window_are_served_with_the_side_kept_who_picked_and_when_the_window_closes()
    {
        using var cli = _h.Cli();
        var at = _h.Slice.Clock.GetUtcNow();
        await _h.Seed(_first);
        var prior = (await _h.Row(_first))!;
        _h.Picks.Remember(_first, new TodoPick(at, prior, prior.Version, Kept: "obsidian", By: "human:ui"));
        _h.Picks.RememberHold(new TodoHoldPick(at, "applied", [], By: "session:abc"));

        var note = await Read();

        Assert.Equal([("conflict", _first.Value, "obsidian", "human:ui", at + TodoPickWindow.Span), ("hold", null, "applied", "session:abc", at + TodoPickWindow.Span)],
            note.Picks.Select(p => (p.Kind, p.Id?.Value, p.Kept, p.By, p.UndoUntil)));
    }

    [Fact]
    public async Task A_pick_whose_window_has_closed_is_not_served()
    {
        using var cli = _h.Cli();
        await _h.Seed(_first);
        var prior = (await _h.Row(_first))!;
        _h.Picks.Remember(_first, new TodoPick(_h.Slice.Clock.GetUtcNow() - TodoPickWindow.Span - TimeSpan.FromSeconds(1), prior, prior.Version, Kept: "sindri", By: "human:ui"));

        var note = await Read();

        Assert.Empty(note.Picks);
    }

    [Fact]
    public async Task Note_lines_that_break_the_text_rules_are_served_with_their_number_and_reason()
    {
        using var cli = _h.Cli();
        _h.NoteFile.Current = new TodoNoteSnapshot("sha256:aa", NoteParser.Parse("# Todo\n- [ ] fine one\n- [ ] see [the doc](https://example.com/a)\n- [ ] \n"));

        var note = await Read();

        Assert.Equal([(3, "it has a markdown link"), (4, "it has no text")], note.CantSync.Select(c => (c.Line, c.Reason)));
    }
}
