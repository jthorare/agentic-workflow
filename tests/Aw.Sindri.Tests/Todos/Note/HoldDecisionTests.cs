using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>When a pass holds what it counted (todo spec §4.5 "The hold", REQ-E7): more than 3 AND more than 10% of the base's open todos (exactly 10% applies).</summary>
[Lane(TestLane.Unit)]
public sealed class HoldDecisionTests
{
    private static List<HeldChange> Counted(int count) =>
        [.. Enumerable.Range(1, count).Select(static n => Gone(n))];

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(1, 1, false)]
    [InlineData(3, 6, false)]
    [InlineData(3, 10, false)]
    [InlineData(4, 4, true)]
    [InlineData(4, 6, true)]
    [InlineData(4, 10, true)]
    [InlineData(4, 50, false)]
    [InlineData(8, 100, false)]
    [InlineData(10, 100, false)]
    [InlineData(11, 100, true)]
    [InlineData(40, 400, false)]
    [InlineData(41, 400, true)]
    public void Held_only_past_three_and_past_ten_percent_of_open_todos(int count, int baseOpen, bool held)
        => Assert.Equal(held, HoldDecision.Decide(Counted(count), baseOpen).Held);

    [Fact]
    public void The_decision_carries_the_counted_changes_as_given()
    {
        var counted = Counted(5);

        Assert.Equal(counted, HoldDecision.Decide(counted, 10).Changes);
    }
}
