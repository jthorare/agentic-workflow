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

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        RenderCount++;
        context.DrawRectangle(Brushes.Transparent, pen: null, new Rect(Bounds.Size));   // so a drag on empty space hits this control
        if (Graph is not { } graph)
        {
            return;
        }

        if (graph.IsEmpty)
        {
            var empty = Text(graph.EmptyText, 13, Brush("Brush.InkMuted"), Math.Max(Bounds.Width - 48, 0), semiBold: false);
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
    }

    private void DrawEdge(DrawingContext context, GraphEdgeViewModel edge)
    {
        var (start, c1, c2, end) = Curve(edge);
        var wire = Brush("Brush.Wire");
        var curve = new StreamGeometry();
        using (var ctx = curve.Open())
        {
            ctx.BeginFigure(start, isFilled: false);
            ctx.CubicBezierTo(c1, c2, end);
            ctx.EndFigure(isClosed: false);
        }

        context.DrawGeometry(brush: null, new Pen(wire, 2), curve);

        // The arrow head points along the curve's last stretch (from the second control point to the end).
        var angle = Math.Atan2(end.Y - c2.Y, end.X - c2.X);
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

        if (edge.Label is { } label)
        {
            var text = Text(label, 11, Brush("Brush.Ink"), 80, semiBold: true);
            var mid = new Point((start.X + (3 * c1.X) + (3 * c2.X) + end.X) / 8, (start.Y + (3 * c1.Y) + (3 * c2.Y) + end.Y) / 8);
            var pill = new Rect(mid.X - (text.Width / 2) - 6, mid.Y - (text.Height / 2) - 2, text.Width + 12, text.Height + 4);
            context.DrawRectangle(Brush("Brush.Surface1"), new Pen(Brush("Brush.Hairline"), 1), pill, 4, 4);
            context.DrawText(text, new Point(pill.X + 6, pill.Y + 2));
        }
    }

    private void DrawEnd(DrawingContext context, GraphEnd end)
    {
        var faint = Brush("Brush.InkSubtle");
        context.DrawLine(new Pen(faint, 1, DashStyle.Dash), new Point(end.At.X, end.At.Y), new Point(end.To.X, end.To.Y));
        var text = Text(end.Text, 11, faint, end.Label.Width, semiBold: false);
        context.DrawText(text, new Point(end.Label.X, end.Label.Y + ((end.Label.Height - text.Height) / 2)));
    }

    private void DrawNode(DrawingContext context, GraphNodeViewModel node)
    {
        var rect = WorldRect(node);
        if (node.IsSelected)
        {
            context.DrawRectangle(brush: null, new Pen(Brush("Brush.Primary"), 2), rect.Inflate(RingGap), Radius + RingGap, Radius + RingGap);
        }

        context.DrawRectangle(Brush("Brush.Surface2"), new Pen(Brush("Brush.Hairline"), 1), rect, Radius, Radius);
        var width = rect.Width - 24;
        context.DrawText(Text(node.Title, 13, Brush("Brush.Ink"), width, semiBold: true), new Point(rect.X + 12, rect.Y + 8));
        context.DrawText(Text(node.Type, 11, Brush("Brush.InkMuted"), width, semiBold: false), new Point(rect.X + 12, rect.Y + 27));
        if (node.StatusWord.Length > 0)
        {
            context.DrawText(Text(node.Glyph + " " + node.StatusWord, 11, Brush(ToneKey(node.Tone)), width, semiBold: false), new Point(rect.X + 12, rect.Y + 44));
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

    private static Rect WorldRect(GraphNodeViewModel node) => new(node.X, node.Y, WorkflowGraphViewModel.CardWidth, WorkflowGraphViewModel.CardHeight);

    private Rect ToScreen(Rect world) => new((world.X * Zoom) + Pan.X, (world.Y * Zoom) + Pan.Y, world.Width * Zoom, world.Height * Zoom);

    private Point ToScreen(Point world) => new((world.X * Zoom) + Pan.X, (world.Y * Zoom) + Pan.Y);

    private Point ToWorld(Point screen) => new((screen.X - Pan.X) / Zoom, (screen.Y - Pan.Y) / Zoom);

    private static FormattedText Text(string text, double size, IBrush brush, double maxWidth, bool semiBold) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, semiBold ? FontWeight.SemiBold : FontWeight.Normal), size, brush)
        {
            MaxTextWidth = maxWidth,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
