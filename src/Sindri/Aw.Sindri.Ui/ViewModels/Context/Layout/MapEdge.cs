namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

/// <summary>One edge as the edges layer draws it.</summary>
/// <param name="Key">A key unique among the edges.</param>
/// <param name="Route">The path, from a port on one node to a port on the other.</param>
/// <param name="Dashed">Whether the edge is suggested and drawn dashed.</param>
/// <param name="BrushKey">The resource key of the stroke brush; Brush.Wire when null.</param>
/// <param name="HitWidth">The width of the stroke the pointer hits, px.</param>
/// <param name="Arrow">Whether the edge ends in an arrow head (#3234: the run canvas shows direction; the context map does not).</param>
/// <param name="Label">The text of the pill at the edge's midpoint, or null for none.</param>
internal sealed record MapEdge(string Key, EdgeRoute Route, bool Dashed, string? BrushKey = null, double HitWidth = MapEdge.DefaultHitWidth, bool Arrow = false, string? Label = null)
{
    /// <summary>The hit stroke's default width, px: wide enough that a 2 px line is easy to land on.</summary>
    internal const double DefaultHitWidth = 12;
}
