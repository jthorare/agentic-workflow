namespace Aw.Sindri.Ui.Views;

/// <summary>Where the Settings page scrolls to when a step is asked for: the title stays in view whenever the step fits under it.</summary>
internal static class RevealScroll
{
    /// <summary>The scroll offset that shows a step.</summary>
    /// <param name="rowTop">The step's top edge, from the top of the page.</param>
    /// <param name="rowBottom">The step's bottom edge, from the top of the page.</param>
    /// <param name="viewportHeight">How tall the page's visible area is.</param>
    /// <returns>0 when the step is in the first screenful, else the step's top.</returns>
    internal static double Offset(double rowTop, double rowBottom, double viewportHeight) => throw new NotSupportedException();
}
