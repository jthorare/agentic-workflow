using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The graph canvas (<see cref="GraphCanvas"/>, continued): drawing the cards, the arrows and their branch labels.</summary>
internal sealed partial class GraphCanvas
{
    private const double ArrowSize = 9;
    private const double Radius = 8;
    private const double RingGap = 3;

    private readonly List<(string Text, double Size)> _drawn = [];

    /// <summary>The words the last pass drew, each with its font size: a test reads it to see that the text size setting reaches the canvas (#3231).</summary>
    internal IReadOnlyList<(string Text, double Size)> DrawnText => _drawn;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        RenderCount++;
        _drawn.Clear();
        context.DrawRectangle(Brushes.Transparent, pen: null, new Rect(Bounds.Size));   // so a drag on empty space hits this control
        if (Graph is not { } graph)
        {
            return;
        }

        if (graph.IsEmpty)
        {
            var empty = Text(graph.EmptyText, 13 * TextScale, Brush("Brush.InkMuted"), Math.Max(Bounds.Width - 48, 0), semiBold: false);
            context.DrawText(empty, new Point(24, 24));
            return;
        }

        using (context.PushTransform(Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(Pan.X, Pan.Y)))
        {
            foreach (var edge in graph.Edges)
            {
                DrawEdge(context, edge);
            }

            foreach (var end in graph.Ends)
            {
                DrawEnd(context, end);
            }

            foreach (var node in graph.Nodes)
            {
                DrawNode(context, node);
            }
        }

        DrawScrollbars(context);
    }

    private void DrawEdge(DrawingContext context, GraphEdgeViewModel edge)
    {
        var (start, c1, c2, end) = Curve(edge);
        var wire = Brush("Brush.Wire");
        context.DrawGeometry(brush: null, new Pen(wire, 2, lineJoin: PenLineJoin.Round), EdgePath(edge, start, c1, c2, end));

        // The arrow head points along the path's last stretch (from the second control point, or the last corner, to the end).
        var from = edge.Route.Count > 0 ? new Point(edge.Route[^1].X, edge.Route[^1].Y) : c2;
        DrawArrowHead(context, wire, from, end);
        if (edge.Label is { } label)
        {
            DrawEdgeLabel(context, label, new Point((start.X + (3 * c1.X) + (3 * from.X) + end.X) / 8, (start.Y + (3 * c1.Y) + (3 * from.Y) + end.Y) / 8));
        }
    }

    // One curve for an edge inside a row; a wrapped edge runs along its row's return lane (#3684): straight lines through the right pad, the row gap and the left pad, never across a card.
    private static StreamGeometry EdgePath(GraphEdgeViewModel edge, Point start, Point c1, Point c2, Point end)
    {
        var path = new StreamGeometry();
        using var ctx = path.Open();
        ctx.BeginFigure(start, isFilled: false);
        if (edge.Route.Count > 0)
        {
            foreach (var corner in edge.Route)
            {
                ctx.LineTo(new Point(corner.X, corner.Y));
            }

            ctx.LineTo(end);
        }
        else
        {
            ctx.CubicBezierTo(c1, c2, end);
        }

        ctx.EndFigure(isClosed: false);
        return path;
    }

    private static void DrawArrowHead(DrawingContext context, IBrush wire, Point from, Point end)
    {
        var angle = Math.Atan2(end.Y - from.Y, end.X - from.X);
        Point Wing(double turn) => new(end.X - (ArrowSize * Math.Cos(angle + turn)), end.Y - (ArrowSize * Math.Sin(angle + turn)));
        var head = new StreamGeometry();
        using (var ctx = head.Open())
        {
            ctx.BeginFigure(end, isFilled: true);
            ctx.LineTo(Wing(0.45));
            ctx.LineTo(Wing(-0.45));
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(wire, pen: null, head);
    }

    // The branch output's pill at the middle of the curve, at the text scale the cards were laid out at (#3231).
    private void DrawEdgeLabel(DrawingContext context, string label, Point mid)
    {
        var k = _laidOutScale;
        var text = Text(label, 11 * k, Brush("Brush.Ink"), 80 * k, semiBold: true);
        var pill = new Rect(mid.X - (text.Width / 2) - (6 * k), mid.Y - (text.Height / 2) - (2 * k), text.Width + (12 * k), text.Height + (4 * k));
        context.DrawRectangle(Brush("Brush.Surface1"), new Pen(Brush("Brush.Hairline"), 1), pill, 4 * k, 4 * k);
        context.DrawText(text, new Point(pill.X + (6 * k), pill.Y + (2 * k)));
    }

    // Thin scrollbars, only on an axis that overflows: a thumb showing where the viewport is in the scroll extent.
    private void DrawScrollbars(DrawingContext context)
    {
        if (!Scrolls)
        {
            return;
        }

        var world = World;
        var extentW = world.Right * Zoom;
        var extentH = GraphZoom.ExtentHeight(world.Bottom, Zoom, mapShown: true);
        var thumb = Brush("Brush.InkSubtle");
        if (extentH > Bounds.Height + 0.5)
        {
            var h = Math.Max(MinHandle, Bounds.Height * Bounds.Height / extentH);
            var y = -Pan.Y / (extentH - Bounds.Height) * (Bounds.Height - h);
            context.DrawRectangle(thumb, pen: null, new Rect(Bounds.Width - 8, y, 4, h), 2, 2);
        }

        if (extentW > Bounds.Width + 0.5)
        {
            var w = Math.Max(MinHandle, Bounds.Width * Bounds.Width / extentW);
            var x = -Pan.X / (extentW - Bounds.Width) * (Bounds.Width - w);
            context.DrawRectangle(thumb, pen: null, new Rect(x, Bounds.Height - 8, w, 4), 2, 2);
        }
    }

    private void DrawEnd(DrawingContext context, GraphEnd end)
    {
        var faint = Brush("Brush.InkSubtle");
        context.DrawLine(new Pen(faint, 1, DashStyle.Dash), new Point(end.At.X, end.At.Y), new Point(end.To.X, end.To.Y));
        var text = Text(end.Text, 11 * _laidOutScale, faint, end.Label.Width, semiBold: false);
        context.DrawText(text, new Point(end.Label.X, end.Label.Y + ((end.Label.Height - text.Height) / 2)));
    }

    private void DrawNode(DrawingContext context, GraphNodeViewModel node)
    {
        var rect = WorldRect(node);
        if (node.IsSelected)
        {
            context.DrawRectangle(brush: null, new Pen(Brush("Brush.Primary"), 2), SelectedRing(rect), Radius + RingGap + 1, Radius + RingGap + 1);
        }

        context.DrawRectangle(Brush("Brush.Surface2"), new Pen(Brush("Brush.Hairline"), 1), rect, Radius, Radius);
        var k = node.Scale;
        var chip = Graph?.ChipFor(node);
        var chipText = chip is null ? null : Text(chip, 11 * k, Brush("Brush.InkMuted"), rect.Width / 3, semiBold: false);
        var chipWidth = chipText is null ? 0 : chipText.Width + (16 * k);
        var iconSize = GraphBlockIcons.Size * k;
        using (context.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(rect.X + (12 * k), rect.Y + ((rect.Height - iconSize) / 2))))
        {
            context.DrawGeometry(brush: null, new Pen(Brush("Brush.InkMuted"), 1.5, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), GraphBlockIcons.For(node.Type));
        }

        var textX = rect.X + (12 * k) + iconSize + (12 * k);   // the icon, then a 12 px gap
        var width = rect.Right - (12 * k) - textX - (chipWidth > 0 ? chipWidth + (8 * k) : 0);
        context.DrawText(Text(node.Title, 13 * k, Brush("Brush.Ink"), width, semiBold: true), new Point(textX, rect.Y + (8 * k)));
        if (chipText is not null)
        {
            var box = new Rect(rect.Right - (12 * k) - chipWidth, rect.Y + (6 * k), chipWidth, chipText.Height + (4 * k));
            context.DrawRectangle(brush: null, new Pen(Brush("Brush.Hairline"), 1), box, box.Height / 2, box.Height / 2);
            context.DrawText(chipText, new Point(box.X + (8 * k), box.Y + (2 * k)));
        }

        var lineWidth = rect.Right - (12 * k) - textX;
        context.DrawText(Text(node.Line.Length > 0 ? node.Line : node.Type, 11 * k, Brush("Brush.InkMuted"), lineWidth, semiBold: false), new Point(textX, rect.Y + (27 * k)));
        if (node.StatusWord.Length > 0)
        {
            context.DrawText(Text(node.Glyph + " " + node.StatusWord, 11 * k, Brush(ToneKey(node.Tone)), lineWidth, semiBold: false), new Point(textX, rect.Bottom - (16 * k)));
        }
    }

#pragma warning disable IDE0072 // None and Muted share the subtle ink
    private static string ToneKey(GraphTone tone) => tone switch
    {
        GraphTone.Success => "Brush.Success",
        GraphTone.Error => "Brush.Error",
        GraphTone.Attention => "Brush.Attention",
        _ => "Brush.InkSubtle",
    };
#pragma warning restore IDE0072

    private static (Point Start, Point C1, Point C2, Point End) Curve(GraphEdgeViewModel edge)
    {
        var (start, end) = (new Point(edge.Start.X, edge.Start.Y), new Point(edge.End.X, edge.End.Y));
        var reach = Math.Max(MinHandle, Math.Abs(end.X - start.X) / 2);
        return (start, new Point(start.X + reach, start.Y), new Point(end.X - reach, end.Y), end);
    }

    /// <summary>The path the selected card's ring is stroked along: the pen is centred on it, so its inner edge is 3 px outside the card's border.</summary>
    /// <param name="card">The card's rectangle.</param>
    /// <returns>The rectangle to stroke.</returns>
    internal static Rect SelectedRing(Rect card) => card.Inflate(RingGap + 1);

    private static Rect WorldRect(GraphNodeViewModel node) => new(node.X, node.Y, node.Width, node.Height);

    private Rect ToScreen(Rect world) => new((world.X * Zoom) + Pan.X, (world.Y * Zoom) + Pan.Y, world.Width * Zoom, world.Height * Zoom);

    private Point ToScreen(Point world) => new((world.X * Zoom) + Pan.X, (world.Y * Zoom) + Pan.Y);

    private Point ToWorld(Point screen) => new((screen.X - Pan.X) / Zoom, (screen.Y - Pan.Y) / Zoom);

    private FormattedText Text(string text, double size, IBrush brush, double maxWidth, bool semiBold)
    {
        _drawn.Add((text, size));
        return new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, semiBold ? FontWeight.SemiBold : FontWeight.Normal), size, brush)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
    }

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
