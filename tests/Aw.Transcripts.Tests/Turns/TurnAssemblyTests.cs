using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Turns;

namespace Aw.Transcripts.Tests.Turns;

public sealed class TurnAssemblyTests
{
    private static readonly SessionRef _session = SessionRef.From("transcript:0a1b2c3d");
    private static readonly DateTimeOffset _t = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Lane", "unit")]
    public void Assistant_lines_up_to_the_next_prompt_are_one_turn_with_each_tool_once()
    {
        var turns = TurnAssembler.Assemble(_session,
        [
            new(1, _t, EntryKind.Human, "go", []),
            new(2, _t.AddSeconds(1), EntryKind.Assistant, "one", ["Read"]),
            new(4, _t.AddSeconds(2), EntryKind.Assistant, "", ["Read", "Edit"]),
            new(5, _t.AddSeconds(3), EntryKind.Assistant, "two", []),
            new(6, _t.AddSeconds(4), EntryKind.Human, "next", []),
        ]);

        Assert.Equal<string>(["transcript:0a1b2c3d#1 Human go", "transcript:0a1b2c3d#2 Assistant one\ntwo Read,Edit", "transcript:0a1b2c3d#6 Human next"],
            turns.Select(t => $"{t.Ref.Value} {t.Role} {t.Text}{(t.Tools.Count > 0 ? " " + string.Join(',', t.Tools) : "")}"), StringComparer.Ordinal);
        Assert.Equal(_t.AddSeconds(1), turns[1].At);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_machine_prompt_ends_the_assistant_turn_and_is_no_turn_itself()
    {
        var turns = TurnAssembler.Assemble(_session,
        [
            new(1, _t, EntryKind.Assistant, "before", []),
            new(2, _t, EntryKind.MachinePrompt, "", []),
            new(3, _t, EntryKind.Assistant, "after", []),
        ]);

        Assert.Equal<string>(["before", "after"], turns.Select(t => t.Text), StringComparer.Ordinal);
        Assert.All(turns, t => Assert.Equal(TurnRole.Assistant, t.Role));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_copied_turn_is_dropped_and_a_file_never_dedupes_against_itself()
    {
        var original = TurnAssembler.Assemble(_session, [new(1, _t, EntryKind.Human, "fix  it", []), new(2, _t, EntryKind.Human, "fix  it", [])]);
        var copy = TurnAssembler.Assemble(_session, [new(1, _t, EntryKind.Human, "fix it\n", []), new(2, _t.AddMinutes(1), EntryKind.Human, "fix it", [])]);

        var own = TurnDedupe.Own([.. copy.Select(TurnKey.Of)], [[.. original.Select(TurnKey.Of)]]);
        var self = TurnDedupe.Own([.. original.Select(TurnKey.Of)], []);

        Assert.Equal([false, true], own);
        Assert.Equal([true, true], self);
    }

    [Fact, Trait("Lane", "unit")]
    public void Files_order_by_first_time_then_last_then_path()
    {
        var a = new FileSpan(_t, _t.AddMinutes(5), "/b");
        var longer = new FileSpan(_t, _t.AddMinutes(9), "/a");
        var later = new FileSpan(_t.AddMinutes(1), _t.AddMinutes(2), "/a");
        var none = new FileSpan(First: null, Last: null, "/a");

        Assert.Equal([a, longer, later, none], new[] { none, later, longer, a }.Order(EarlierFileFirst.Instance));
        Assert.True(TurnDedupe.CanHoldOriginals(a, _t.AddMinutes(5), longer));
        Assert.False(TurnDedupe.CanHoldOriginals(longer, _t.AddMinutes(9), a));
        Assert.False(TurnDedupe.CanHoldOriginals(a, _t.AddSeconds(-1), longer));
    }
}
