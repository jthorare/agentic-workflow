namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One column's width: pixels, or a share of the free width (<see cref="IsStar"/>), never narrower than <see cref="Min"/>. A hidden column is 0 pixels wide and keeps its place.</summary>
/// <param name="Value">Pixels, or the star share.</param>
/// <param name="IsStar">True for a share of the free width.</param>
/// <param name="Min">The narrowest it may be.</param>
internal readonly record struct ColumnSize(double Value, bool IsStar, double Min);
