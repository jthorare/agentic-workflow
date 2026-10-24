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
        var hits = Map?.HitKeys ?? new HashSet<string>(StringComparer.Ordinal);
        var zoomed = Map?.IsZoomed == true;
        foreach (var edge in drawn)
        {
            var lit = WholeEdgeSelection.Touches(edge, _focus);
            using (context.PushOpacity(EdgeOpacity(edge, zoomed, hits)))
            {
                context.DrawLine(EdgePen(WholeEdgeStyle.Of(edge).Dashed, lit), new Point(edge.X1, edge.Y1), new Point(edge.X2, edge.Y2));
            }
        }

        if (WholeMapViewModel.UsesDots(Scale))
        {
            return;
        }

        foreach (var edge in drawn.Where(e => WholeEdgeSelection.ShowsPill(e, drawn.Count, _focus)))
        {
            DrawPill(context, WholeEdgeStyle.Of(edge), (edge.X1 + edge.X2) / 2, (edge.Y1 + edge.Y2) / 2);
        }
    }

    // One line per pair of areas with its count, never one per node edge.
    private void DrawAreaBundles(DrawingContext context, WholeMapGeometry geometry)
    {
        foreach (var bundle in WholeAreaBundle.Of(geometry.AreaEdges))
        {
            using var _ = context.PushOpacity(Touches(bundle.From, bundle.To) ? 1 : Faded(EdgeFade));
            var width = (1 + Math.Min(4, Math.Log2(bundle.Count + 1))) / Math.Max(Scale, 0.2);
            var pen = new Pen(Brush(WholeEdgeStyle.Of(bundle).Dashed ? "Brush.Wire" : "Brush.WireHi"), width, WholeEdgeStyle.Of(bundle).Dashed ? new DashStyle([6, 4], 0) : null);
            context.DrawLine(pen, new Point(bundle.X1, bundle.Y1), new Point(bundle.X2, bundle.Y2));
        }
    }

    // Drawn after the tiles, so a tile never covers its line's count.
    private void DrawBundlePills(DrawingContext context, WholeMapGeometry geometry)
    {
        foreach (var bundle in WholeAreaBundle.Of(geometry.AreaEdges))
        {
            DrawPill(context, WholeEdgeStyle.Of(bundle), (bundle.X1 + bundle.X2) / 2, (bundle.Y1 + bundle.Y2) / 2);
        }
    }

    private Pen EdgePen(bool dashed, bool lit) =>
        new(Brush(lit ? "Brush.WireHi" : dashed ? "Brush.Wire" : "Brush.WireHi"), (lit ? 2 * Stroke : Stroke) / Math.Max(Scale, 0.2), dashed ? new DashStyle([6, 4], 0) : null);

    // The mockup's pill: a small card on the edge, its border dashed for a suggestion.
    private void DrawPill(DrawingContext context, WholeEdgeStyle style, double x, double y)
    {
        var size = PillSize / Math.Max(Scale, 0.5);
        var text = new FormattedText(style.Pill, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), size, Brush("Brush.InkSubtle"));
        var pad = 5 / Math.Max(Scale, 0.5);
        var rect = new Rect(x - (text.Width / 2) - pad, y - (text.Height / 2) - (pad / 2), text.Width + (2 * pad), text.Height + pad);
        context.DrawRectangle(Brush("Brush.Surface1"), new Pen(Brush("Brush.Hairline"), 1 / Math.Max(Scale, 0.5), style.Dashed ? new DashStyle([3, 2], 0) : null), rect, radiusX: 6, radiusY: 6);
        context.DrawText(text, new Point(rect.X + pad, rect.Y + (pad / 2)));
    }
}
