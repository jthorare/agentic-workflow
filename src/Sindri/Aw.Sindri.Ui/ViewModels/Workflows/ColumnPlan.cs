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
    internal static ColumnPlan For(double width)
    {
        const double Glyph = 24, LastRun = 170, State = 72, Gap = 16, Margins = 32;
        var (trigger, next, slots) = width switch
        {
            >= 1100 => (200d, 130d, 10),
            >= 900 => (200d, 0d, 10),   // around 1000 px the Next run column drops first
            _ => (0d, 0d, 5),   // below 900 the strip shows the last 5 runs, then Trigger drops
        };
        var strip = slots == 10 ? 140d : 70d;
        var others = new[] { Glyph, trigger, LastRun, next, strip, State };
        var shown = others.Count(w => w > 0) + 1;   // and the Name column
        var name = Math.Clamp(width - others.Sum() - (Gap * (shown - 1)) - Margins, 200, 360);
        return new ColumnPlan(name, trigger, LastRun, next, slots);
    }

    /// <summary>The strip column's width: 10 slots, or 5 when narrow.</summary>
    public double Strip => StripSlots == 10 ? 140 : 70;
}
