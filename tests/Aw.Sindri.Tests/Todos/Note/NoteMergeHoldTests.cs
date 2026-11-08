using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>The hold inside the merge (todo spec §4.5 "The hold", REQ-E7; the pure half of TP-12).</summary>
[Lane(TestLane.Unit)]
public sealed class NoteMergeHoldTests
{
    private static readonly string[] _ten = [.. Enumerable.Range(1, 10).Select(static n => L(n, Txt("t", n)))];
    private static readonly SindriTodo[] _tenInSindri = [.. Enumerable.Range(1, 10).Select(static n => S(n, Txt("t", n)))];

    private static NoteMergeResult Run(string[] noteBase, string[] note, SindriTodo[] sindri, NoteMergeOptions? options = null) =>
        NoteMerge.Merge(Base(noteBase), Doc(note), sindri, options ?? Options());

    private static NoteHold HoldOn(params int[] numbers) => new([.. numbers.Select(Gone)]);

    private static string[] Without(params int[] numbers) => [.. _ten.Where((_, i) => !numbers.Contains(i + 1))];

    private static void SameSet(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Same(expected.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));

    [Fact]
    public void Four_removed_lines_are_held_and_none_is_applied()
    {
        var r = Run(_ten, Without(1, 2, 3, 4), _tenInSindri);

        Assert.Empty(r.Intents);
        Assert.Equal(NoteHoldOutcome.Opened, r.Hold);
        Assert.Equal(
            HoldOn(1, 2, 3, 4).Changes,
            r.HoldNow!.Changes);
    }

    [Fact]
    public void Held_ids_keep_their_old_base_lines()
    {
        var r = Run(_ten, Without(1, 2, 3, 4), _tenInSindri);

        Same(_ten, r.NextBase);
    }

    [Fact]
    public void Exactly_three_removed_lines_are_not_held()
    {
        var r = Run(_ten, Without(1, 2, 3), _tenInSindri);

        SameSet(["remove 1", "remove 2", "remove 3"], Intents(r));
        Assert.Equal(NoteHoldOutcome.None, r.Hold);
        Assert.Null(r.HoldNow);
    }

    [Fact]
    public void Ticks_adds_and_edits_of_lines_that_stay_done_still_apply_while_a_hold_is_open()
    {
        var done = X(11, "d");
        var note = new[] { X(5, "t5"), L(6, "t6"), L(7, "t7"), L(8, "t8"), L(9, "t9"), L(10, "t10"), X(11, "d2"), "- [ ] new" };

        var r = Run([.. _ten, done], note, [.. _tenInSindri, S(11, "d") with { Done = true }]);

        Assert.Equal(NoteHoldOutcome.Opened, r.Hold);
        SameSet(["done 5", "change 11 text=\"d2\"", "adopt 9000 \"new\" line 8"], Intents(r));
    }

    [Fact]
    public void Other_todos_keep_syncing_while_a_hold_is_open()
    {
        var sindri = _tenInSindri.Select(static t => t.Id == Id(6) ? t with { Text = "t6 from sindri" } : t).ToArray();

        var r = Run(_ten, Without(1, 2, 3, 4), sindri);

        Same([$"replace 2: {L(6, "t6 from sindri")}"], Edits(r));
        Assert.Contains(L(6, "t6 from sindri"), r.NextBase, StringComparer.Ordinal);
        Assert.Contains(L(1, "t1"), r.NextBase, StringComparer.Ordinal);
    }

    [Fact]
    public void Four_open_todos_edited_are_held_with_the_notes_lines()
    {
        var note = _ten.Select(static (l, i) => i < 4 ? L(i + 1, Txt("edited ", i + 1)) : l).ToArray();

        var r = Run(_ten, note, _tenInSindri);

        Assert.Empty(r.Intents);
        Assert.Equal(L(2, "edited 2"), r.HoldNow!.Changes.Single(static c => c.Id == Id(2)).NoteLine);
        Assert.All(r.HoldNow.Changes, static c => Assert.Equal(HeldKind.Edited, c.Kind));
    }

    [Fact]
    public void Three_edits_are_applied()
    {
        var note = _ten.Select(static (l, i) => i < 3 ? L(i + 1, Txt("edited ", i + 1)) : l).ToArray();

        var r = Run(_ten, note, _tenInSindri);

        Assert.Equal(3, r.Intents.OfType<NoteChange>().Count());
        Assert.Equal(NoteHoldOutcome.None, r.Hold);
    }

    [Fact]
    public void Four_done_todos_set_back_to_open_are_held_as_reopened()
    {
        var done = Enumerable.Range(1, 4).Select(static n => X(n, "d")).Concat(Enumerable.Range(5, 6).Select(static n => L(n, Txt("t", n)))).ToArray();
        var note = Enumerable.Range(1, 4).Select(static n => L(n, "d")).Concat(Enumerable.Range(5, 6).Select(static n => L(n, Txt("t", n)))).ToArray();
        var sindri = Enumerable.Range(1, 4).Select(static n => S(n, "d") with { Done = true }).Concat(Enumerable.Range(5, 6).Select(static n => S(n, Txt("t", n)))).ToArray();

        var r = Run(done, note, sindri);

        Assert.Empty(r.Intents);
        Assert.All(r.HoldNow!.Changes, static c => Assert.Equal(HeldKind.Reopened, c.Kind));
        Assert.Equal(4, r.HoldNow.Changes.Count);
    }

    [Fact]
    public void Two_removals_and_two_edits_together_make_four_and_are_held()
    {
        var note = new[] { L(3, "e3"), L(4, "e4") }.Concat(_ten.Skip(4)).ToArray();

        var r = Run(_ten, note, _tenInSindri);

        Assert.Empty(r.Intents);
        Assert.Equal([HeldKind.Removed, HeldKind.Removed, HeldKind.Edited, HeldKind.Edited], r.HoldNow!.Changes.Select(static c => c.Kind).Order());
    }

    [Fact]
    public void Four_open_todos_in_the_base_with_three_edited_apply_because_the_half_rule_starts_at_six()
    {
        var four = new[] { L(1, "a"), L(2, "b"), L(3, "c"), L(4, "d") };
        var note = new[] { L(1, "a2"), L(2, "b2"), L(3, "c2"), L(4, "d") };

        var r = Run(four, note, [S(1, "a"), S(2, "b"), S(3, "c"), S(4, "d")]);

        Assert.Equal(3, r.Intents.OfType<NoteChange>().Count());
        Assert.Equal(NoteHoldOutcome.None, r.Hold);
    }

    [Fact]
    public void Six_open_lines_ticked_in_one_save_all_apply()
    {
        var six = Enumerable.Range(1, 6).Select(static n => L(n, Txt("t", n))).ToArray();
        var ticked = Enumerable.Range(1, 6).Select(static n => X(n, Txt("t", n))).ToArray();

        var r = Run(six, ticked, [.. Enumerable.Range(1, 6).Select(static n => S(n, Txt("t", n)))]);

        Assert.Equal(6, r.Intents.OfType<NoteDone>().Count());
        Assert.Equal(NoteHoldOutcome.None, r.Hold);
    }

    [Fact]
    public void Twenty_ticked_lines_cleared_out_never_ask()
    {
        var done = Enumerable.Range(1, 20).Select(static n => X(n, Txt("d", n))).ToArray();
        var sindri = Enumerable.Range(1, 20).Select(static n => S(n, Txt("d", n)) with { Done = true }).ToArray();

        var r = Run(done, [], sindri);

        Assert.Equal(20, r.Intents.OfType<NoteRemove>().Count());
        Assert.Equal(NoteHoldOutcome.None, r.Hold);
    }

    [Fact]
    public void The_same_set_still_held_appends_nothing_new()
    {
        var open = HoldOn(1, 2, 3, 4);

        var r = Run(_ten, Without(1, 2, 3, 4), _tenInSindri, Options(o => o with { OpenHold = open }));

        Assert.Equal(NoteHoldOutcome.Unchanged, r.Hold);
        Assert.Empty(r.Intents);
    }

    [Fact]
    public void A_different_set_replaces_the_open_hold()
    {
        var open = HoldOn(1, 2, 3, 4);

        var r = Run(_ten, Without(1, 2, 3, 4, 5), _tenInSindri, Options(o => o with { OpenHold = open }));

        Assert.Equal(NoteHoldOutcome.Replaced, r.Hold);
        Assert.Equal(5, r.HoldNow!.Changes.Count);
    }

    [Fact]
    public void Lines_put_back_by_hand_dissolve_the_hold_and_the_rest_apply()
    {
        var open = HoldOn(1, 2, 3, 4);

        var r = Run(_ten, Without(1, 2), _tenInSindri, Options(o => o with { OpenHold = open }));

        Assert.Equal(NoteHoldOutcome.Dissolved, r.Hold);
        Assert.Null(r.HoldNow);
        SameSet(["remove 1", "remove 2"], Intents(r));
        Same(_ten.Skip(2), r.NextBase);
    }

    [Fact]
    public void Held_edits_keep_their_old_base_lines()
    {
        var note = _ten.Select(static (l, i) => i < 4 ? L(i + 1, Txt("edited ", i + 1)) : l).ToArray();

        var r = Run(_ten, note, _tenInSindri);

        Same(_ten, r.NextBase);
    }

    [Fact]
    public void Held_reopened_todos_keep_their_old_base_lines()
    {
        var baseLines = Enumerable.Range(1, 4).Select(static n => X(n, "d")).Concat(Enumerable.Range(5, 6).Select(static n => L(n, Txt("t", n)))).ToArray();
        var note = Enumerable.Range(1, 4).Select(static n => L(n, "d")).Concat(baseLines.Skip(4)).ToArray();
        var sindri = Enumerable.Range(1, 4).Select(static n => S(n, "d") with { Done = true }).Concat(_tenInSindri.Skip(4)).ToArray();

        var r = Run(baseLines, note, sindri);

        Same(baseLines, r.NextBase);
    }

    [Fact]
    public void A_hold_that_shrinks_but_stays_over_the_threshold_is_replaced_with_the_smaller_set()
    {
        var open = HoldOn(1, 2, 3, 4, 5);

        var r = Run(_ten, Without(2, 3, 4, 5), _tenInSindri, Options(o => o with { OpenHold = open }));

        Assert.Equal(NoteHoldOutcome.Replaced, r.Hold);
        Assert.Equal(HoldOn(2, 3, 4, 5).Changes, r.HoldNow!.Changes);
        Assert.Empty(r.Intents);
        Same(_ten, r.NextBase);
    }
}
