using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's drawing (<see cref="WholeMapCanvas"/>, continued): the why / product / code lanes, a chip in the look of its kind, and the overview's columns.</summary>
internal sealed partial class WholeMapCanvas
{
    private const double MarkSize = 7;
    private const double MarkInset = 10;
    private const double LaneTitleSize = 13;

    // A lane per band across the whole world, under the columns' contents: its title and what it holds on the strip over its rows.
    private void DrawLanes(DrawingContext context, WholeMapGeometry geometry, WholeRect view, bool dots)
    {
        var fill = Brush("Brush.LaneFill");
        var line = new Pen(Brush("Brush.Hairline"), 1);
        var muted = Brush("Brush.InkSubtle");
        foreach (var band in geometry.Bands.Where(b => new WholeRect(0, b.LaneTop, geometry.World.Width, b.LaneBottom - b.LaneTop).Intersects(view)))
        {
            var lane = new Rect(0, band.LaneTop, geometry.World.Width, band.LaneBottom - band.LaneTop);
            context.DrawRectangle(fill, line, lane, radiusX: Radius, radiusY: Radius);
            var accent = Brush(BandAccent(band.Band));
            var size = dots ? LaneTitleSize / Math.Max(Scale, 0.25) : LaneTitleSize;
            var title = Text(NodeBands.Title(band.Band), size, accent, 240, bold: true);
            context.DrawText(title, new Point(WholeMapLayout.Margin / 2, band.LaneTop + 3));
            if (band.Meta.Length > 0)
            {
                context.DrawText(Text(band.Meta, size * 0.9, muted, Math.Max(80, geometry.World.Width - 360)), new Point((WholeMapLayout.Margin / 2) + title.WidthIncludingTrailingWhitespace + 12, band.LaneTop + 3));
            }
        }
    }

    private static string BandAccent(WholeBand band) => band switch
    {
        WholeBand.Concept => NodeBands.StyleOf("concept").Accent,
        WholeBand.Intent => NodeBands.StyleOf("decision").Accent,
        WholeBand.Product => NodeBands.StyleOf("feature").Accent,
        WholeBand.Code => NodeBands.StyleOf("code").Accent,
        WholeBand.Other => NodeBands.StyleOf(string.Empty).Accent,
        _ => NodeBands.StyleOf(string.Empty).Accent,
    };

    // A chip in its kind's look: pill for the why, card for the product, plate for code; the kind's mark in front of the label; the border stays the area's colour.
    private void DrawKindChip(DrawingContext context, KindChip chip)
    {
        var (node, area, fill, ink, focused) = (chip.Node, chip.Area, chip.Fill, chip.Ink, chip.Focused);
        var style = NodeBands.StyleOf(node.Kind);
        var radius = style.Shape switch
        {
            ChipShape.Pill => node.Rect.Height / 2,
            ChipShape.Plate => 2,
            ChipShape.Card => Radius,
            _ => Radius,
        };
        var rect = new Rect(node.Rect.X, node.Rect.Y, node.Rect.Width, node.Rect.Height);
        context.DrawRectangle(fill, new Pen(area, focused ? 2.8 : node.Confirmed ? 1.6 : 1, node.Confirmed ? null : new DashStyle([3, 2], 0)), rect, radiusX: radius, radiusY: radius);
        DrawMark(context, style.Mark, Brush(style.Accent), new Point(node.Rect.X + MarkInset, node.Rect.CenterY));
        var inset = style.Mark == KindMark.None ? 6 : MarkInset + MarkSize + 4;
        context.DrawText(Text(node.Label, TextSize, ink, node.Rect.Width - inset - 8), new Point(node.Rect.X + inset, node.Rect.Y + 6));
    }

    private static void DrawMark(DrawingContext context, KindMark mark, IBrush brush, Point at)
    {
        var half = MarkSize / 2;
        switch (mark)
        {
            case KindMark.Dot:
                context.DrawEllipse(brush, pen: null, at, half, half);
                break;
            case KindMark.Square:
                context.DrawRectangle(brush, pen: null, new Rect(at.X - half, at.Y - half, MarkSize, MarkSize), radiusX: 1, radiusY: 1);
                break;
            case KindMark.Bar:
                context.DrawRectangle(brush, pen: null, new Rect(at.X - 1.5, at.Y - MarkSize, 3, 2 * MarkSize), radiusX: 1, radiusY: 1);
                break;
            case KindMark.Diamond:
                context.DrawGeometry(brush, pen: null, Polygon([new(at.X, at.Y - half - 1), new(at.X + half + 1, at.Y), new(at.X, at.Y + half + 1), new(at.X - half - 1, at.Y)]));
                break;
            case KindMark.Triangle:
                context.DrawGeometry(brush, pen: null, Polygon([new(at.X, at.Y - half - 1), new(at.X + half + 1, at.Y + half), new(at.X - half - 1, at.Y + half)]));
                break;
            case KindMark.Chevron:
                context.DrawLine(new Pen(brush, 1.8), new Point(at.X - half, at.Y - half), new Point(at.X + half, at.Y));
                context.DrawLine(new Pen(brush, 1.8), new Point(at.X + half, at.Y), new Point(at.X - half, at.Y + half));
                break;
            case KindMark.None:
            default:
                break;
        }
    }

    private static StreamGeometry Polygon(Point[] points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: true);
            foreach (var point in points.Skip(1))
            {
                ctx.LineTo(point);
            }

            ctx.EndFigure(isClosed: true);
        }

        return geometry;
    }

    // An area's column on the overview: its name on top, then a line per kind counted in the band the kind belongs to (the lanes run behind it).
    private void DrawTileColumn(DrawingContext context, PlacedTile placed, bool focused)
    {
        var (colour, pattern) = WholePalette.For(placed.AreaIndex);
        var area = Brush(PaletteKey(colour));
        var rect = new Rect(placed.Rect.X, placed.Rect.Y, placed.Rect.Width, placed.Rect.Height);
        context.DrawRectangle(Brush("Brush.Surface1"), new Pen(area, focused ? 3.2 : 1.6), rect, radiusX: 10, radiusY: 10);
        DrawSwatch(context, rect, area, pattern);
        context.DrawText(Text(placed.Label, 13, Brush("Brush.Ink"), rect.Width - 40, bold: true), new Point(rect.X + 32, rect.Y + 6));
        foreach (var cell in placed.BandCells)
        {
            var fit = Math.Max(1, (int)(cell.Rect.Height / WholeMapLayout.KindLine));
            for (var i = 0; i < Math.Min(fit, cell.Kinds.Count); i++)
            {
                var (kind, count) = (cell.Kinds[i].Key, cell.Kinds[i].Value);
                var style = NodeBands.StyleOf(kind);
                var y = cell.Rect.Y + (i * WholeMapLayout.KindLine);
                DrawMark(context, style.Mark, Brush(style.Accent), new Point(cell.Rect.X + MarkInset, y + (WholeMapLayout.KindLine / 2)));
                var words = i == fit - 1 && cell.Kinds.Count > fit ? string.Create(CultureInfo.InvariantCulture, $"+{cell.Kinds.Count - i} more kinds") : ViewModels.Labels.ContextWholeKindCount(kind, count);
                context.DrawText(Text(words, TextSize, Brush("Brush.Ink"), cell.Rect.Width - MarkInset - MarkSize - 14), new Point(cell.Rect.X + MarkInset + MarkSize + 4, y + 2));
            }
        }
    }
}
