namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One edge between two placed nodes, as a line between their centres.</summary>
/// <param name="From">The source key.</param>
/// <param name="To">The target key.</param>
/// <param name="Type">The edge type.</param>
/// <param name="Confirmed">True when confirmed (drawn solid; a suggested edge is dashed).</param>
/// <param name="X1">The line's start x.</param>
/// <param name="Y1">The line's start y.</param>
/// <param name="X2">The line's end x.</param>
/// <param name="Y2">The line's end y.</param>
/// <param name="Confidence">How sure the tool is, 0 to 1 (shown as a percentage on a suggested edge's pill).</param>
internal sealed record PlacedEdge(string From, string To, string Type, bool Confirmed, double X1, double Y1, double X2, double Y2, double Confidence = 1);
