using Avalonia;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>One fact cell the flat Row drew (#3189): its column key, its rectangle in the row, the text it shows and how wide that text is laid out.</summary>
/// <param name="Key">The column key.</param>
/// <param name="Bounds">The cell, in the row's coordinates.</param>
/// <param name="Text">The text the cell shows (a ref is already cut to fit).</param>
/// <param name="TextWidth">The laid-out width of that text.</param>
internal sealed record RowCell(string Key, Rect Bounds, string Text, double TextWidth);
