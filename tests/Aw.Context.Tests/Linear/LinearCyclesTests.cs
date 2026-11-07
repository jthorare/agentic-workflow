using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;

namespace Aw.Context.Tests.Linear;

/// <summary>Which cycles the picker offers: the current one, the next one and the last three (linear hub spec 2026-11-04 §4.2).</summary>
[Trait("Lane", "unit")]
public sealed class LinearCyclesTests
{
    private static readonly DateTimeOffset _start = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private static LinearLookupCycle Cycle(int number) =>
        new(ExternalId.From("cycle-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)), number, Name: null, _start.AddDays(14 * (number - 1)), _start.AddDays(14 * number));

    private static int[] Numbers(IEnumerable<LinearLookupCycle> cycles) => [.. cycles.Select(static c => c.Number)];

    [Fact]
    public void The_current_cycle_the_next_one_and_the_last_three_are_kept_oldest_first()
    {
        var cycles = Enumerable.Range(1, 8).Select(Cycle);
        var during5 = _start.AddDays((14 * 4) + 3);

        Assert.Equal([2, 3, 4, 5, 6], Numbers(LinearCycles.Pick(cycles, during5)));
    }

    [Fact]
    public void A_team_with_no_cycle_yet_running_gets_the_next_one_only() => Assert.Equal([1], Numbers(LinearCycles.Pick([Cycle(1), Cycle(2)], _start.AddDays(-1))));

    [Fact]
    public void The_last_cycle_running_has_no_next_one()
    {
        var cycles = Enumerable.Range(1, 5).Select(Cycle);

        Assert.Equal([2, 3, 4, 5], Numbers(LinearCycles.Pick(cycles, _start.AddDays(14 * 4))));
    }

    [Fact]
    public void A_team_whose_cycles_are_all_over_gets_the_last_three()
    {
        var cycles = Enumerable.Range(1, 5).Select(Cycle);

        Assert.Equal([3, 4, 5], Numbers(LinearCycles.Pick(cycles, _start.AddDays(14 * 6))));
    }

    [Fact]
    public void No_cycles_is_none() => Assert.Empty(LinearCycles.Pick([], _start));
}
