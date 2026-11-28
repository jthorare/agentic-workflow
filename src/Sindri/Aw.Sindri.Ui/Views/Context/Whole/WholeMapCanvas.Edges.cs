using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's edges (<see cref="WholeMapCanvas"/>, continued; q86): bundled lines between areas, node edges only inside an area or around the focus, and their pills.</summary>
internal sealed partial class WholeMapCanvas
{
    private const double PillSize = 11;
    private const double Stroke = 1.2;

    private void DrawNodeEdges(DrawingContext context, WholeMapGeometry geometry, WholeRect view)
    {
        var drawn = WholeEdgeSelection.Visible(geometry.Edges, geometry.Nodes, _focus).Where(e => Overlaps(view, e.X1, e.Y1, e.X2, e.Y2)).ToList();
        foreach (var edge in drawn)
        {
            var lit = WholeEdgeSelection.Touches(edge, _focus);
            using (context.PushOpacity(EdgeOpacity(edge)))
            {
                context.DrawLine(EdgePen(WholeEdgeStyle.Of(edge).Dashed, lit), new Point(edge.X1, edge.Y1), new Point(edge.X2, edge.Y2));
                DrawLinkEmphasis(context, edge);
            }
        }

        if (WholeMapViewModel.UsesDots(Scale))
        {
            return;
        }

        foreach (var pill in WholePillLayout.Place(geometry, drawn, _focus, Scale, PillBox))
        {
            DrawPill(context, pill.Style, pill.X, pill.Y);   // no label lies on another, none under a node (#3678)
        }
    }

    private (double Width, double Height) PillBox(string pill)
    {
        var text = PillText(pill);
        var pad = PillPad;
        return (text.Width + (2 * pad), text.Height + pad);
    }

    private double PillPad => 5 / Math.Max(Scale, 0.5);

    private FormattedText PillText(string pill) =>
        new(pill, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), PillSize / Math.Max(Scale, 0.5), Brush("Brush.InkSubtle"));

    private Pen EdgePen(bool dashed, bool lit) =>
        new(Brush(lit ? "Brush.WireHi" : dashed ? "Brush.Wire" : "Brush.WireHi"), (lit ? 2 * Stroke : Stroke) / Math.Max(Scale, 0.2), dashed ? new DashStyle([6, 4], 0) : null);

    // The mockup's pill: a small card on the edge, its border dashed for a suggestion.
    private void DrawPill(DrawingContext context, WholeEdgeStyle style, double x, double y)
    {
        var text = PillText(style.Pill);
        var pad = PillPad;
        var rect = new Rect(x - (text.Width / 2) - pad, y - (text.Height / 2) - (pad / 2), text.Width + (2 * pad), text.Height + pad);
        context.DrawRectangle(Brush("Brush.Surface1"), new Pen(Brush("Brush.Hairline"), 1 / Math.Max(Scale, 0.5), style.Dashed ? new DashStyle([3, 2], 0) : null), rect, radiusX: 6, radiusY: 6);
        context.DrawText(text, new Point(rect.X + pad, rect.Y + (pad / 2)));
    }
}
