namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>A heading of an opened area's canvas (#3674): a band ("Entry points") or a sub-group of Core ("Requests 13").</summary>
/// <param name="Title">The heading's words.</param>
/// <param name="Caption">What the band holds, in words ("routes, jobs, middleware"); empty for a sub-group.</param>
/// <param name="Count">How many nodes sit under it.</param>
/// <param name="Rect">Where the heading sits.</param>
/// <param name="Sub">True for a sub-group of Core.</param>
internal sealed record PlacedAreaBand(string Title, string Caption, int Count, WholeRect Rect, bool Sub)
{
    /// <summary>Gets whether nothing sits under the heading: it says "none in this area".</summary>
    internal bool IsEmpty => Count == 0 && !Sub;
}
