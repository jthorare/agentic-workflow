using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Context;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Tests.Context.Whole;

/// <summary>Issue 3259: area names order by their numbers (Area 2 before Area 10), not as plain text.</summary>
[Lane(TestLane.Unit)]
public sealed class NaturalOrderTests
{
    private static readonly string[] _names = ["Area 0", "Area 1", "Area 2", "Area 9", "Area 10", "Area 11"];

    [Fact]
    public void Digit_runs_compare_as_numbers()
    {
        var sorted = _names.Reverse().Order(NaturalOrder.Instance).ToList();

        Assert.Equal(_names, sorted, StringComparer.Ordinal);
    }

    [Fact]
    public void Text_ignores_case_and_ties_fall_back_to_ordinal()
    {
        Assert.True(NaturalOrder.Instance.Compare("area 2", "Area 10") < 0);
        Assert.True(NaturalOrder.Instance.Compare("A1", "a1") < 0);
        Assert.Equal(0, NaturalOrder.Instance.Compare("a1", "a1"));
    }

    [Fact]
    public void Areas_run_in_natural_order_so_map_items_and_cards_follow()
    {
        var areas = _names.Reverse().Select(n => new WholeArea(n, n, "trusted", 1, new Dictionary<string, int>(StringComparer.Ordinal), false)).ToList();

        var order = WholeAreaOrder.Of(areas, []);

        Assert.Equal(_names, order, StringComparer.Ordinal);
    }
}
