namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One label on an edge, and where it is to be drawn.</summary>
/// <param name="Style">How its edge is drawn and the label's words.</param>
/// <param name="X">The label's centre, world x.</param>
/// <param name="Y">The label's centre, world y.</param>
internal sealed record WholePill(WholeEdgeStyle Style, double X, double Y);
