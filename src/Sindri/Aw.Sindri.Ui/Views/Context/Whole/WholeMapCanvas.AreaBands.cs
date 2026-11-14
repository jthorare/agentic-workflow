using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>An opened area's canvas (<see cref="WholeMapCanvas"/>, continued; #3674): the band headings and the link under the pointer.</summary>
internal sealed partial class WholeMapCanvas
{
    private const double HaloWidth = 6;
    private const double HaloOpacity = 0.35;
    private PlacedEdge? _hoverLink;
    private PlacedEdge? _selectedLink;

    private void DrawAreaBands(DrawingContext context, WholeMapGeometry geometry, WholeRect view)
    {
        var ink = Brush("Brush.Ink");
        var muted = Brush("Brush.InkSubtle");
        foreach (var band in (geometry.AreaBands ?? []).Where(b => b.Rect.Intersects(view)))
        {
            var rect = band.Rect;
            if (band.Sub)
            {
                context.DrawText(Text(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{band.Title} {band.Count}"), TextSize, muted, rect.Width, bold: true), new Point(rect.X, rect.Y));
                continue;
            }

            var title = Text(band.Title, LaneTitleSize, ink, rect.Width, bold: true);
            context.DrawText(title, new Point(rect.X, rect.Y));
            context.DrawText(Text(band.Caption, TextSize, muted, Math.Max(rect.Width - title.WidthIncludingTrailingWhitespace - 8, 0)), new Point(rect.X + title.WidthIncludingTrailingWhitespace + 8, rect.Y + 1));
            if (band.IsEmpty)
            {
                context.DrawText(Text(Labels.ContextAreaNone, TextSize, muted, rect.Width), new Point(rect.X, rect.Y + 34));
            }
        }
    }

    // The link under a point of the world, if the point is within a few screen px of its line.
    private PlacedEdge? LinkAt(Point at)
    {
        if (Map?.Shown is not { IsAreaCanvas: true } geometry)
        {
            return null;
        }

        var tolerance = 5 / Math.Max(Scale, 0.5);
        return geometry.Edges.FirstOrDefault(e => Distance(at, e) <= tolerance);
    }

    private static double Distance(Point p, PlacedEdge e)
    {
        var (dx, dy) = (e.X2 - e.X1, e.Y2 - e.Y1);
        var length = (dx * dx) + (dy * dy);
        var t = length == 0 ? 0 : Math.Clamp((((p.X - e.X1) * dx) + ((p.Y - e.Y1) * dy)) / length, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - (e.X1 + (t * dx)), 2) + Math.Pow(p.Y - (e.Y1 + (t * dy)), 2));
    }

    // The hovered link gets a halo and 2 px, the selected one 1.5 px, both in the wire colour; the tooltip names "A → B".
    private void HoverLink(Point at)
    {
        var link = Map?.Hovered is null ? LinkAt(at) : null;
        if (link == _hoverLink)
        {
            return;
        }

        _hoverLink = link;
        var tip = link is null || Map?.Shown is not { } geometry ? null : Labels.ContextLinkTip(LabelOf(geometry, link.From), LabelOf(geometry, link.To));
        ToolTip.SetTip(this, tip);
        ToolTip.SetPlacement(this, PlacementMode.Pointer);
        ToolTip.SetHorizontalOffset(this, 8);
        InvalidateVisual();
    }

    private static string LabelOf(WholeMapGeometry geometry, string key) => geometry.Nodes.FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.Ordinal))?.Label ?? key;

    private bool SelectLink(Point at)
    {
        var link = Map?.KeyAt(at.X, at.Y) is null ? LinkAt(at) : null;
        if (link == _selectedLink)
        {
            return false;
        }

        _selectedLink = link;
        InvalidateVisual();
        return link is not null;
    }

    private void DrawLinkEmphasis(DrawingContext context, PlacedEdge edge)
    {
        var wire = Brush("Brush.WireHi");
        var line = (new Point(edge.X1, edge.Y1), new Point(edge.X2, edge.Y2));
        if (edge == _hoverLink)
        {
            using (context.PushOpacity(HaloOpacity))
            {
                context.DrawLine(new Pen(wire, HaloWidth), line.Item1, line.Item2);
            }

            context.DrawLine(new Pen(wire, 2), line.Item1, line.Item2);
        }
        else if (edge == _selectedLink)
        {
            context.DrawLine(new Pen(wire, 1.5), line.Item1, line.Item2);
        }
    }
}
