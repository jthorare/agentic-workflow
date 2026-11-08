using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>When a pass holds what it counted (todo spec §4.5 "The hold", REQ-E7): more than 3, or more than half the base's open todos when the base has 6 or more.</summary>
[Lane(TestLane.Unit)]
public sealed class HoldDecisionTests
{
    private static List<HeldChange> Counted(int count) =>
        [.. Enumerable.Range(1, count).Select(static n => Gone(n))];

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(1, 1, false)]
    [InlineData(3, 10, false)]
    [InlineData(4, 10, true)]
    [InlineData(4, 100, true)]
    [InlineData(3, 4, false)]
    [InlineData(3, 5, false)]
    [InlineData(3, 6, false)]
    [InlineData(4, 6, true)]
    [InlineData(4, 4, true)]
    [InlineData(2, 2, false)]
    public void More_than_three_is_held_and_exactly_three_is_not(int count, int baseOpen, bool held)
        => Assert.Equal(held, HoldDecision.Decide(Counted(count), baseOpen).Held);

    [Fact]
    public void The_decision_carries_the_counted_changes_as_given()
    {
        var counted = Counted(5);

        Assert.Equal(counted, HoldDecision.Decide(counted, 10).Changes);
    }
}
