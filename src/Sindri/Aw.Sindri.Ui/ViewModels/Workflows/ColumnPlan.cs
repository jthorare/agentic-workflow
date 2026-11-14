namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Which columns of the workflow list show at a width, and how wide each is (frames wf-list, wf-list-1000; #3683). Widths are device-independent pixels.</summary>
/// <param name="Name">The Name column's width.</param>
/// <param name="Trigger">The Trigger column's width; 0 when it dropped.</param>
/// <param name="LastRun">The Last run column's width (it never drops).</param>
/// <param name="NextRun">The Next run column's width; 0 when it dropped.</param>
/// <param name="StripSlots">How many of the last runs the strip draws: 10, or 5 below 900.</param>
internal sealed record ColumnPlan(double Name, double Trigger, double LastRun, double NextRun, int StripSlots)
{
    /// <summary>The full list: every column, 10 slots.</summary>
    internal static ColumnPlan Wide { get; } = new(360, 200, 170, 130, 10);

    /// <summary>True when the Trigger column shows.</summary>
    public bool ShowsTrigger => Trigger > 0;

    /// <summary>True when the Next run column shows.</summary>
    public bool ShowsNextRun => NextRun > 0;

    /// <summary>The columns at a width.</summary>
    /// <param name="width">The list's width.</param>
    /// <returns>The plan.</returns>
    internal static ColumnPlan For(double width) => Wide;
}
