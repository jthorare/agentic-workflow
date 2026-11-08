using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>Open conflicts and picks in the merge (todo spec D11, TP-10): nothing syncs for a todo with an open conflict, an agreement closes it, and a pick against a removal is never asked twice.</summary>
[Lane(TestLane.Unit)]
public sealed class NoteMergeConflictTests
{
    private static readonly NoteSide _removed = new(Text: null, Due: null, Links: [], Done: false, Removed: true);

    private static NoteMergeResult Run(string[] noteBase, string[] note, SindriTodo[] sindri, NoteMergeOptions options) =>
        NoteMerge.Merge(Base(noteBase), Doc(note), sindri, options);

    private static NoteMergeOptions Open(NoteSide side) => Options(o => o with { OpenConflicts = new Dictionary<TodoId, NoteSide> { [Id(1)] = side } });

    private static SindriTodo GoneBySindri(string text) => S(1, text) with { Removed = true, RemovedBy = NoteOrigin.Sindri };

    [Fact]
    public void An_open_conflict_whose_note_line_went_back_to_the_base_writes_nothing_and_asks_again_with_the_new_side()
    {
        var r = Run([L(1, "a")], [L(1, "a")], [S(1, "c")], Open(Side("b")));

        Same(["conflict 1 sindri=\"c\" note=\"a\""], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void An_open_conflict_closes_when_the_note_comes_to_what_sindri_has()
    {
        var r = Run([L(1, "a")], [L(1, "c")], [S(1, "c")], Open(Side("b")));

        Same(["settled 1"], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(1, "c")], r.NextBase);
    }

    [Fact]
    public void An_open_conflict_closes_when_both_sides_went_back_to_the_base()
    {
        var r = Run([L(1, "a")], [L(1, "a")], [S(1, "a")], Open(Side("b")));

        Same(["settled 1"], Intents(r));
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void Both_changed_to_the_same_values_closes_the_open_conflict_even_with_no_base()
    {
        var r = NoteMerge.Merge(noteBase: null, Doc(L(1, "c")), [S(1, "c")], Open(Side("b")));

        Same(["settled 1"], Intents(r));
    }

    [Fact]
    public void A_conflict_that_is_not_open_still_settles_with_no_intent()
    {
        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "b")], Options());

        Assert.Empty(r.Intents);
    }

    [Fact]
    public void An_open_conflict_does_not_pass_a_note_edit_on_while_sindri_is_back_at_the_base()
    {
        var r = Run([L(1, "a")], [L(1, "b2")], [S(1, "a")], Open(Side("b")));

        Same(["conflict 1 sindri=\"a\" note=\"b2\""], Intents(r));
    }

    [Fact]
    public void A_removal_by_sindri_with_an_open_conflict_on_the_same_note_side_does_nothing()
    {
        var r = Run([L(1, "a")], [L(1, "b")], [GoneBySindri("a")], Open(Side("b")));

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void A_removal_by_sindri_with_an_open_conflict_does_not_remove_a_line_the_owner_put_back_to_the_base()
    {
        var r = Run([L(1, "a")], [L(1, "a")], [GoneBySindri("a")], Open(Side("b")));

        Same(["conflict 1 sindri=removed note=\"a\""], Intents(r));
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void A_removal_by_sindri_with_an_open_conflict_asks_again_when_the_note_side_moved()
    {
        var r = Run([L(1, "a")], [L(1, "b2")], [GoneBySindri("a")], Open(Side("b")));

        Same(["conflict 1 sindri=removed note=\"b2\""], Intents(r));
    }

    [Fact]
    public void A_line_the_note_removed_with_the_conflict_open_on_the_removal_does_nothing()
    {
        var r = Run([L(1, "a")], [], [S(1, "a2")], Open(_removed));

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void A_line_the_note_removed_with_the_conflict_open_on_an_edit_asks_again_and_removes_nothing_in_sindri()
    {
        var r = Run([L(1, "a")], [], [S(1, "a")], Open(Side("b")));

        Same(["conflict 1 sindri=\"a\" note=removed"], Intents(r));
    }

    [Fact]
    public void Both_sides_removed_closes_the_open_conflict()
    {
        var r = Run([L(1, "a")], [], [GoneBySindri("a")], Open(Side("b")));

        Same(["settled 1"], Intents(r));
        Assert.Empty(r.NextBase);
    }

    [Fact]
    public void Both_sides_removed_with_no_conflict_is_quiet()
    {
        var r = Run([L(1, "a")], [], [GoneBySindri("a")], Options());

        Assert.Empty(r.Intents);
        Assert.Empty(r.NextBase);
    }

    [Fact]
    public void Keep_sindri_against_a_note_removal_puts_the_line_back_and_is_not_asked_again()
    {
        var options = Options(o => o with { PickedRemoved = new HashSet<TodoId> { Id(1) } });

        var r = Run([L(1, "a")], [], [S(1, "a2")], options);

        Assert.Empty(r.Intents);
        Same([$"append: {L(1, "a2")}"], Edits(r));
        Same([L(1, "a2")], r.NextBase);
    }

    [Fact]
    public void Keep_sindri_against_a_note_removal_writes_nothing_while_the_undo_window_is_open()
    {
        var options = Options(o => o with
        {
            PickedRemoved = new HashSet<TodoId> { Id(1) },
            LeaveAlone = new HashSet<TodoId> { Id(1) },
        });

        var r = Run([L(1, "a")], [], [S(1, "a2")], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void Keep_the_removal_against_a_sindri_edit_leaves_the_todo_removed_and_the_note_alone()
    {
        var options = Options(o => o with { PickedRemoved = new HashSet<TodoId> { Id(1) } });

        var r = Run([L(1, "a")], [], [GoneBySindri("a2") with { RemovedBy = NoteOrigin.Obsidian }], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Assert.Empty(r.NextBase);
    }

    [Fact]
    public void Keep_sindri_against_a_sindri_removal_removes_the_note_line_and_is_not_asked_again()
    {
        var options = Options(o => o with { PickBases = new Dictionary<TodoId, string> { [Id(1)] = L(1, "b") } });

        var r = Run([L(1, "a")], [L(1, "b")], [GoneBySindri("a")], options);

        Assert.Empty(r.Intents);
        Same(["remove 1"], Edits(r));
        Assert.Empty(r.NextBase);
    }

    [Fact]
    public void Keep_obsidian_against_a_sindri_removal_with_the_todo_restored_to_the_note_line_does_nothing()
    {
        var options = Options(o => o with { PickBases = new Dictionary<TodoId, string> { [Id(1)] = L(1, "b") } });

        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "b")], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "b")], r.NextBase);
    }
}
