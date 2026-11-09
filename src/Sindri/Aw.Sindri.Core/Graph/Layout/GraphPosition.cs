namespace Aw.Sindri.Core.Graph.Layout;

/// <summary>Where the canvas puts one node: its column (<paramref name="Layer"/>) and row, and the pixel position they give.</summary>
/// <param name="Layer">0 for nodes nothing feeds; one more than the deepest node that feeds it otherwise.</param>
/// <param name="Row">The place among the nodes of the same layer, in spec order.</param>
/// <param name="X">Left edge in canvas units.</param>
/// <param name="Y">Top edge in canvas units.</param>
public sealed record GraphPosition(int Layer, int Row, int X, int Y);
