using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The tick bar's counts (#2779): one tick per record up to 40, then a tick stands for several; the lit share is Value of Total.</summary>
[Lane(TestLane.Unit)]
public sealed class TickBarTests
{
    [Theory]
    [InlineData(30, 10, 30, 10)]
    [InlineData(9111, 0, 40, 0)]
    [InlineData(9111, 9111, 40, 40)]
    [InlineData(9111, 4556, 40, 20)]
    [InlineData(60, 30, 40, 20)]
    [InlineData(0, 0, 0, 0)]
    public void Ticks_and_lit_ticks_follow_total_and_value(int total, int value, int ticks, int lit)
    {
        var bar = new TickBar { Total = total, Value = value };

        Assert.Equal((ticks, lit), (bar.Ticks, bar.Lit));
    }
}
