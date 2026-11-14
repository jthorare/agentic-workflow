using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>#3683 column drop (frames wf-list at 1440, wf-list-1000): wide keeps every column; around 1000 the Next run column drops first; below 900 the strip shows the last 5 runs and Trigger drops too. Name, Last run and the groups never drop.</summary>
[Lane(TestLane.Unit)]
public sealed class ColumnPlanTests
{
    [Theory]
    [InlineData(1392)]
    [InlineData(1100)]
    public void A_wide_list_keeps_every_column_and_ten_strip_slots(double width)
    {
        var plan = ColumnPlan.For(width);

        Assert.Equal((true, true, 10, 170d), (plan.ShowsTrigger, plan.ShowsNextRun, plan.StripSlots, plan.LastRun));
    }

    [Theory]
    [InlineData(1099)]
    [InlineData(952)]
    [InlineData(900)]
    public void Around_1000_the_Next_run_column_drops_first(double width)
    {
        var plan = ColumnPlan.For(width);

        Assert.Equal((true, false, 10), (plan.ShowsTrigger, plan.ShowsNextRun, plan.StripSlots));
    }

    [Theory]
    [InlineData(899)]
    [InlineData(700)]
    public void Below_900_the_strip_shows_five_runs_and_Trigger_drops_but_Name_and_Last_run_stay(double width)
    {
        var plan = ColumnPlan.For(width);

        Assert.Equal((false, false, 5), (plan.ShowsTrigger, plan.ShowsNextRun, plan.StripSlots));
        Assert.True(plan.Name >= 200 && plan.LastRun > 0, "Name and Last run never drop");
    }

    [Theory]
    [InlineData(1392)]
    [InlineData(952)]
    [InlineData(760)]
    public void The_Name_column_is_between_200_and_360_and_the_row_fits_the_width(double width)
    {
        var plan = ColumnPlan.For(width);

        Assert.InRange(plan.Name, 200, 360);
        var strip = plan.StripSlots == 10 ? 140d : 70d;
        var shown = new[] { 24d, plan.Name, plan.Trigger, plan.LastRun, plan.NextRun, strip, 72 }.Where(w => w > 0).ToList();
        var needed = shown.Sum() + (16 * (shown.Count - 1)) + 32;
        Assert.True(needed <= width, $"a row needs {needed} px at {width}");
    }
}
