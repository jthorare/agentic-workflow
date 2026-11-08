using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The three-way merge, one test per row of todo spec §4.5's table, plus the no-base, cap and conflict rules (the pure halves of TP-9 and TP-10). A todo's number n is in its id, so a result reads "change 1 text=…".</summary>
[Lane(TestLane.Unit)]
public sealed class NoteMergeTests
{
    private static NoteMergeResult Run(string[]? noteBase, string[] note, SindriTodo[] sindri, NoteMergeOptions? options = null) =>
        NoteMerge.Merge(noteBase is null ? null : Base(noteBase), Doc(note), sindri, options ?? Options());

    [Fact]
    public void Same_in_note_and_sindri_as_the_base_does_nothing()
    {
        var lines = new[] { L(1, "a"), L(2, "b") };

        var r = Run(lines, lines, [S(1, "a"), S(2, "b")]);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same(lines, r.NextBase);
        Assert.Equal(NoteHoldOutcome.None, r.Hold);
    }

    [Fact]
    public void Note_changed_only_the_text()
    {
        var r = Run([L(1, "a")], [L(1, "a2")], [S(1, "a")]);

        Same(["change 1 text=\"a2\""], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(1, "a2")], r.NextBase);
    }

    [Fact]
    public void Note_changed_only_the_due_date()
    {
        var r = Run([L(1, "a")], [L(1, "a", "2026-11-14")], [S(1, "a")]);

        Same(["change 1 due=2026-11-14"], Intents(r));
    }

    [Fact]
    public void Note_cleared_the_due_date()
    {
        var r = Run([L(1, "a", "2026-11-14")], [L(1, "a")], [S(1, "a", "2026-11-14")]);

        Same(["change 1 due=none"], Intents(r));
    }

    [Fact]
    public void Note_changed_only_the_links()
    {
        var withLink = $"- [ ] a [ENG-12](sindri://linear/ENG-12) {Block(1)}";

        var r = Run([L(1, "a")], [withLink], [S(1, "a")]);

        Same(["change 1 links=[sindri://linear/ENG-12]"], Intents(r));
    }

    [Fact]
    public void Note_ticked_the_todo()
    {
        var r = Run([L(1, "a")], [X(1, "a")], [S(1, "a")]);

        Same(["done 1"], Intents(r));
    }

    [Fact]
    public void Note_ticked_the_todo_with_a_done_date()
    {
        var r = Run([L(1, "a")], [X(1, "a", "2026-11-07")], [S(1, "a")]);

        Same(["done 1 on=2026-11-07"], Intents(r));
    }

    [Fact]
    public void Note_unticked_the_todo()
    {
        var r = Run([X(1, "a")], [L(1, "a")], [S(1, "a") with { Done = true }]);

        Same(["reopen 1"], Intents(r));
    }

    [Fact]
    public void Note_removed_the_line()
    {
        var r = Run([L(1, "a"), L(2, "b")], [L(2, "b")], [S(1, "a"), S(2, "b")]);

        Same(["remove 1"], Intents(r));
        Same([L(2, "b")], r.NextBase);
    }

    [Fact]
    public void Sindri_changed_only_the_text_so_the_line_is_rewritten()
    {
        var r = Run([L(1, "a")], [L(1, "a")], [S(1, "a2")]);

        Assert.Empty(r.Intents);
        Same([$"replace 1: {L(1, "a2")}"], Edits(r));
        Same([L(1, "a2")], r.NextBase);
    }

    [Fact]
    public void Sindri_checked_the_todo_off_so_the_line_is_rewritten_with_its_date()
    {
        var done = S(1, "a") with { Done = true, DoneOn = D("2026-11-07") };

        var r = Run([L(1, "a")], [L(1, "a")], [done]);

        Same([$"replace 1: {X(1, "a", "2026-11-07")}"], Edits(r));
    }

    [Fact]
    public void Sindri_removed_the_todo_so_the_line_is_removed()
    {
        var removed = S(1, "a") with { Removed = true, RemovedBy = NoteOrigin.Sindri };

        var r = Run([L(1, "a")], [L(1, "a")], [removed]);

        Assert.Empty(r.Intents);
        Same(["remove 1"], Edits(r));
        Assert.Empty(r.NextBase);
    }

    [Fact]
    public void Both_changed_to_the_same_values_does_nothing_and_the_base_catches_up()
    {
        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "b")]);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "b")], r.NextBase);
    }

    [Fact]
    public void Both_changed_differently_is_a_conflict_and_the_note_line_is_left_as_it_is()
    {
        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "c")]);

        Same(["conflict 1 sindri=\"c\" note=\"b\""], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void A_conflict_is_per_todo_so_done_in_sindri_and_text_in_the_note_conflict()
    {
        var done = S(1, "a") with { Done = true };

        var r = Run([L(1, "a")], [L(1, "b")], [done]);

        Same(["conflict 1 sindri=\"a\" note=\"b\""], Intents(r));
    }

    [Fact]
    public void Removed_in_the_note_and_edited_in_sindri_is_a_conflict_with_the_note_side_removed()
    {
        var r = Run([L(1, "a")], [], [S(1, "a2")]);

        Same(["conflict 1 sindri=\"a2\" note=removed"], Intents(r));
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void Removed_in_sindri_and_edited_in_the_note_is_a_conflict_with_the_sindri_side_removed()
    {
        var removed = S(1, "a") with { Removed = true, RemovedBy = NoteOrigin.Sindri };

        var r = Run([L(1, "a")], [L(1, "b")], [removed]);

        Same(["conflict 1 sindri=removed note=\"b\""], Intents(r));
    }

    [Fact]
    public void After_keep_sindri_the_base_is_the_note_line_so_sindri_is_the_one_that_changed()
    {
        var options = Options(o => o with { PickBases = new Dictionary<TodoId, string> { [Id(1)] = L(1, "b") } });

        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "c")], options);

        Assert.Empty(r.Intents);
        Same([$"replace 1: {L(1, "c")}"], Edits(r));
        Same([L(1, "c")], r.NextBase);
    }

    [Fact]
    public void After_keep_obsidian_sindri_equals_the_note_so_nothing_happens()
    {
        var options = Options(o => o with { PickBases = new Dictionary<TodoId, string> { [Id(1)] = L(1, "b") } });

        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "b")], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "b")], r.NextBase);
    }

    [Fact]
    public void A_known_id_on_a_line_that_breaks_D3_is_held_and_nothing_changes()
    {
        var pasted = $"- [ ] a [docs](https://x.example/y) {Block(1)}";

        var r = Run([L(1, "a")], [pasted], [S(1, "a")]);

        Same(["blocked 1 line 1: it has a markdown link"], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void A_new_line_is_adopted_with_a_minted_id_written_onto_it()
    {
        var r = Run([], ["- [ ] buy milk"], []);

        Same(["adopt 9000 \"buy milk\" line 1"], Intents(r));
        Same([$"replace 1: {L(9000, "buy milk")}"], Edits(r));
        Same([L(9000, "buy milk")], r.NextBase);
        Assert.Equal(1, r.Adopted);
    }

    [Fact]
    public void A_bare_line_with_the_text_of_a_done_todo_is_still_a_new_todo()
    {
        var done = S(1, "buy milk") with { Done = true };

        var r = Run([X(1, "buy milk")], [X(1, "buy milk"), "- [ ] buy milk"], [done]);

        Same(["adopt 9000 \"buy milk\" line 2"], Intents(r));
    }

    [Fact]
    public void An_adopted_line_keeps_the_owners_raw_text()
    {
        var r = Run([], ["- [ ] read [[a link]] today  "], []);

        Same(["adopt 9000 \"read [[a link]] today\" line 1"], Intents(r));
        Same([$"replace 1: - [ ] read [[a link]] today {Block(9000)}"], Edits(r));
    }

    [Fact]
    public void A_line_with_a_malformed_id_is_a_new_line_and_gets_a_good_one()
    {
        var r = Run([], ["- [ ] short id ^td-01jc5a8xq0n4w7ms2t9r3vbk6"], []);

        Same(["adopt 9000 \"short id\" line 1"], Intents(r));
        Same([$"replace 1: {L(9000, "short id")}"], Edits(r));
    }

    [Fact]
    public void When_an_id_is_on_two_lines_the_first_keeps_it_and_the_copy_is_adopted()
    {
        var r = Run([L(1, "a")], [L(1, "a"), L(1, "a")], [S(1, "a")]);

        Same(["adopt 9000 \"a\" line 2"], Intents(r));
        Same([$"replace 2: {L(9000, "a")}"], Edits(r));
        Same([L(1, "a"), L(9000, "a")], r.NextBase);
    }

    [Fact]
    public void An_id_sindri_does_not_know_is_adopted_with_that_id_and_the_line_is_not_touched()
    {
        var r = Run([], [L(7, "from a crash")], []);

        Same(["add 7 \"from a crash\""], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(7, "from a crash")], r.NextBase);
    }

    [Fact]
    public void A_line_for_a_todo_removed_in_sindri_that_matches_its_last_line_is_a_stale_copy_and_is_dropped_again()
    {
        var removed = S(1, "a") with { Removed = true, RemovedBy = NoteOrigin.Sindri, LastLineHash = NoteLineHash.Of(L(1, "a")) };

        var r = Run([], [L(1, "a")], [removed]);

        Assert.Empty(r.Intents);
        Same(["remove 1"], Edits(r));
        Assert.Empty(r.NextBase);
    }

    [Fact]
    public void A_different_line_for_a_todo_removed_in_sindri_is_an_owner_re_add_restored_then_edited()
    {
        var removed = S(1, "a") with { Removed = true, RemovedBy = NoteOrigin.Sindri, LastLineHash = NoteLineHash.Of(L(1, "a")) };

        var r = Run([], [L(1, "a edited")], [removed]);

        Same(["restore 1", "change 1 text=\"a edited\""], Intents(r));
        Assert.Empty(r.LineEdits);
        Same([L(1, "a edited")], r.NextBase);
    }

    [Fact]
    public void A_line_that_comes_back_after_the_note_removed_it_is_restored_even_when_identical()
    {
        var removed = S(1, "a") with { Removed = true, RemovedBy = NoteOrigin.Obsidian, LastLineHash = NoteLineHash.Of(L(1, "a")) };

        var r = Run([], [L(1, "a")], [removed]);

        Same(["restore 1"], Intents(r));
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void With_no_base_unknown_lines_are_adopted_and_sindris_todos_are_appended()
    {
        var r = Run(noteBase: null, ["- [ ] bare"], [S(2, "from sindri")]);

        Same(["adopt 9000 \"bare\" line 1"], Intents(r));
        Same([$"replace 1: {L(9000, "bare")}", $"append: {L(2, "from sindri")}"], Edits(r));
        Same([L(9000, "bare"), L(2, "from sindri")], r.NextBase);
    }

    [Fact]
    public void With_no_base_nothing_is_ever_removed()
    {
        var r = Run(noteBase: null, [], [S(1, "a"), S(2, "b")]);

        Assert.Empty(r.Intents);
        Assert.DoesNotContain(r.LineEdits, static e => e.Kind == NoteLineEditKind.Remove);
        Same([$"append: {L(1, "a")}", $"append: {L(2, "b")}"], Edits(r));
    }

    [Fact]
    public void With_no_base_the_same_text_on_both_sides_is_nothing_and_differing_text_is_a_conflict()
    {
        var same = Run(noteBase: null, [L(1, "a")], [S(1, "a")]);
        var differs = Run(noteBase: null, [L(1, "b")], [S(1, "a")]);

        Assert.Empty(same.Intents);
        Same(["conflict 1 sindri=\"a\" note=\"b\""], Intents(differs));
        Assert.Empty(differs.LineEdits);
    }

    [Fact]
    public void At_most_200_lines_are_adopted_in_one_pass_and_the_rest_wait()
    {
        var bare = Enumerable.Range(1, 205).Select(static i => Txt("- [ ] item ", i)).ToArray();

        var r = Run([], bare, []);

        Assert.Equal(200, r.Adopted);
        Assert.Equal(5, r.Deferred);
        Assert.Equal(200, r.Intents.OfType<NoteAdopt>().Count());
        Assert.Equal(200, r.LineEdits.Count);
        Assert.Equal(200, r.NextBase.Count);
        Assert.DoesNotContain(r.LineEdits, static e => e.Number > 200);
    }

    [Fact]
    public void An_open_conflict_with_the_same_note_side_again_adds_nothing_and_keeps_the_base_line()
    {
        var options = Options(o => o with { OpenConflicts = new Dictionary<TodoId, NoteSide> { [Id(1)] = Side("b") } });

        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "c")], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void An_open_conflict_whose_note_side_changed_again_is_one_new_conflict()
    {
        var options = Options(o => o with { OpenConflicts = new Dictionary<TodoId, NoteSide> { [Id(1)] = Side("b") } });

        var r = Run([L(1, "a")], [L(1, "b2")], [S(1, "c")], options);

        Same(["conflict 1 sindri=\"c\" note=\"b2\""], Intents(r));
    }

    [Fact]
    public void While_a_conflict_is_open_neither_side_syncs_that_todo()
    {
        var options = Options(o => o with { OpenConflicts = new Dictionary<TodoId, NoteSide> { [Id(1)] = Side("b") } });

        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "c") with { Done = true }], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
    }

    [Fact]
    public void A_pick_whose_window_is_open_gets_no_line_edit_and_keeps_its_old_base_line()
    {
        var options = Options(o => o with
        {
            PickBases = new Dictionary<TodoId, string> { [Id(1)] = L(1, "b") },
            LeaveAlone = new HashSet<TodoId> { Id(1) },
        });

        var r = Run([L(1, "a")], [L(1, "b")], [S(1, "c")], options);

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "a")], r.NextBase);
    }

    [Fact]
    public void A_todo_added_in_sindri_is_appended_and_the_base_gains_its_line()
    {
        var r = Run([L(1, "a")], [L(1, "a"), string.Empty, "prose"], [S(1, "a"), S(2, "new")]);

        Assert.Empty(r.Intents);
        Same([$"append: {L(2, "new")}"], Edits(r));
        Same([L(1, "a"), L(2, "new")], r.NextBase);
    }

    [Fact]
    public void Text_is_compared_scrubbed_on_both_sides_and_the_base_is_saved_scrubbed()
    {
        var scrubber = new ReplaceScrubber("hunter2", "***");

        var r = Run([L(1, "pw ***")], [L(1, "pw hunter2")], [S(1, "pw ***")], Options(scrubber: scrubber));

        Assert.Empty(r.Intents);
        Assert.Empty(r.LineEdits);
        Same([L(1, "pw ***")], r.NextBase);
    }

    [Fact]
    public void Intents_for_a_secret_in_the_note_carry_the_scrubbed_text()
    {
        var scrubber = new ReplaceScrubber("hunter2", "***");

        var r = Run([L(1, "a")], [L(1, "pw hunter2")], [S(1, "a")], Options(scrubber: scrubber));

        Same(["change 1 text=\"pw ***\""], Intents(r));
    }
}
