namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One node placed on the whole map.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Label">The label.</param>
/// <param name="Kind">The node kind.</param>
/// <param name="AreaIndex">The index of its area in <see cref="WholeMapGeometry.Columns"/>.</param>
/// <param name="Rect">Where it sits.</param>
/// <param name="Confirmed">True when a person confirmed it.</param>
/// <param name="Confidence">How sure the map is of it, 0 to 1; the list under the map sorts by it.</param>
internal sealed record PlacedNode(string Key, string Label, string Kind, int AreaIndex, WholeRect Rect, bool Confirmed, double Confidence = 0);
