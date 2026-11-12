using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;
using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until a page places the control

/// <summary>
/// The layer that draws a graph's edges (the context map, F3 spec 1 §2; the run canvas, F3 spec 3 §2) in one Render pass: a
/// 2 px stroke in Wire (or the edge's own brush key), dashed 8 4 when suggested, a 10 px port with a 3 px radius at each end,
/// WireHi for edges around the hovered or selected node (lit edges and their ports draw at double width, so the lit state
/// does not rest on colour alone), and a hit stroke (12 px unless the edge sets its width) so a thin line
/// is easy to hover. Edges it is told are dimmed draw at 42 %.
/// </summary>
internal sealed class GraphEdgesControl : Control, ICustomHitTest
{
    /// <summary>The edges to draw.</summary>
    internal static readonly StyledProperty<IReadOnlyList<MapEdge>> EdgesProperty =
        AvaloniaProperty.Register<GraphEdgesControl, IReadOnlyList<MapEdge>>(nameof(Edges), []);

    /// <summary>The keys of the edges drawn in WireHi.</summary>
    internal static readonly StyledProperty<IReadOnlySet<string>> HighlightedProperty =
        AvaloniaProperty.Register<GraphEdgesControl, IReadOnlySet<string>>(nameof(Highlighted), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The keys of the edges drawn dimmed.</summary>
    internal static readonly StyledProperty<IReadOnlySet<string>> DimmedProperty =
        AvaloniaProperty.Register<GraphEdgesControl, IReadOnlySet<string>>(nameof(Dimmed), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The key of the edge under the pointer, or null.</summary>
    internal static readonly StyledProperty<string?> HoveredProperty = AvaloniaProperty.Register<GraphEdgesControl, string?>(nameof(Hovered));

    private const string DefaultBrushKey = "Brush.Wire";
    private const double Stroke = 2;
    private const double PortSize = 10;   // outer, the 1 px outline included: the rectangle itself is 9 so the outline lands on whole pixels
    private const double PortRadius = 3;
    private const double DimmedOpacity = 0.42;
    private const double ArrowSize = 9;
    private const double LabelSize = 11;
    private const double LabelMaxWidth = 80;

    private static readonly double[] _dash = [4, 2];   // Pen dashes count in stroke widths: 8 4 at 2 px

    private IReadOnlyList<(MapEdge Edge, StreamGeometry Geometry)> _built = [];

    static GraphEdgesControl()
    {
        AffectsRender<GraphEdgesControl>(HighlightedProperty, DimmedProperty, HoveredProperty);
        _ = EdgesProperty.Changed.AddClassHandler<GraphEdgesControl>((c, _) => c.Rebuild());
    }

    /// <summary>Gets or sets the edges to draw.</summary>
    internal IReadOnlyList<MapEdge> Edges
    {
        get => GetValue(EdgesProperty);
        set => SetValue(EdgesProperty, value);
    }

    /// <summary>Gets or sets the keys of the edges drawn in WireHi.</summary>
    internal IReadOnlySet<string> Highlighted
    {
        get => GetValue(HighlightedProperty);
        set => SetValue(HighlightedProperty, value);
    }

    /// <summary>Gets or sets the keys of the edges drawn dimmed.</summary>
    internal IReadOnlySet<string> Dimmed
    {
        get => GetValue(DimmedProperty);
        set => SetValue(DimmedProperty, value);
    }

    /// <summary>Gets or sets the key of the edge under the pointer.</summary>
    internal string? Hovered
    {
        get => GetValue(HoveredProperty);
        set => SetValue(HoveredProperty, value);
    }

    /// <summary>The key of the topmost edge whose 12 px hit stroke holds a point.</summary>
    /// <param name="point">A point in this control.</param>
    /// <returns>The edge's key, or null when no edge is there.</returns>
    internal string? EdgeAt(Point point)
    {
        for (var i = _built.Count - 1; i >= 0; i--)
        {
            if (_built[i].Edge.Route.DistanceTo(point.ToMap()) <= _built[i].Edge.HitWidth / 2)
            {
                return _built[i].Edge.Key;
            }
        }

        return null;
    }

    /// <summary>The pill behind an edge's label, in this control's coordinates.</summary>
    /// <param name="key">The edge's key.</param>
    /// <returns>The pill, or null when the edge has no label or does not exist.</returns>
    internal Rect? LabelPill(string key) =>
        Edges.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.Ordinal)) is { Label: { } label } edge ? PillFor(edge, LabelText(label, Brushes.Black)) : null;

    /// <summary>The three corners of an edge's arrow head, the tip first (#3234).</summary>
    /// <param name="key">The edge's key.</param>
    /// <returns>The corners, or null when the edge has no arrow or does not exist.</returns>
    internal Point[]? ArrowHead(string key) =>
        Edges.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.Ordinal)) is { Arrow: true } edge ? HeadFor(edge) : null;

    // The tip is the route's end; the wings trail back along its last stretch, 0.45 rad either side, ArrowSize long.
    private static Point[] HeadFor(MapEdge edge)
    {
        var line = edge.Route.Flatten();
        var end = edge.Route.End.ToAvalonia();
        var before = line.Count > 1 ? line[^2].ToAvalonia() : edge.Route.Start.ToAvalonia();
        var angle = Math.Atan2(end.Y - before.Y, end.X - before.X);
        Point Wing(double turn) => new(end.X - (ArrowSize * Math.Cos(angle + turn)), end.Y - (ArrowSize * Math.Sin(angle + turn)));
        return [end, Wing(-0.45), Wing(0.45)];
    }

    private static FormattedText LabelText(string label, IBrush brush) =>
        new(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), LabelSize, brush) { MaxTextWidth = LabelMaxWidth, MaxTextHeight = LabelSize * 2 };

    private static Rect PillFor(MapEdge edge, FormattedText text)
    {
        var mid = edge.Route.Midpoint().ToAvalonia();
        return new Rect(mid.X - (text.Width / 2) - 6, mid.Y - (text.Height / 2) - 2, text.Width + 12, text.Height + 4);
    }

    /// <summary>The stroke width an edge draws at: 2 px, or double when lit.</summary>
    /// <param name="key">The edge's key.</param>
    /// <returns>The width in px.</returns>
    internal double StrokeWidth(string key) => Highlighted.Contains(key) || string.Equals(key, Hovered, StringComparison.Ordinal) ? 2 * Stroke : Stroke;

    /// <summary>The opacity an edge draws at: 0.42 when dimmed, otherwise 1.</summary>
    /// <param name="key">The edge's key.</param>
    /// <returns>The opacity.</returns>
    internal double StrokeOpacity(string key) => Dimmed.Contains(key) ? DimmedOpacity : 1;

    /// <inheritdoc/>
    public bool HitTest(Point point) => EdgeAt(point) is not null;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var (wire, hi, card) = (Brush(DefaultBrushKey), Brush("Brush.WireHi"), Brush("Brush.Surface1"));
        var ports = DrawStrokes(context, wire, hi);

        DrawArrowsAndLabels(context, wire, hi);

        // The ports go on after every stroke, so no later edge paints over one.
        foreach (var (at, (lit, dim)) in ports)
        {
            using (dim ? context.PushOpacity(DimmedOpacity) : default)
            {
                // The outline is 1 px (rectangle 9, so its edge lands on a half pixel and the outline on whole ones) or, lit, 2 px
                // (rectangle 8, edge on a whole pixel): both keep the 10 px outer size.
                var outline = lit ? 2 : 1;
                var size = PortSize - outline;
                var half = size / 2;
                context.DrawRectangle(card, new Pen(lit ? hi : wire, outline), new Rect(at.X - half, at.Y - half, size, size), PortRadius, PortRadius);
            }
        }
    }

    // Arrow heads and label pills, over the strokes and under the ports; they dim with their edge.
    private void DrawArrowsAndLabels(DrawingContext context, IBrush wire, IBrush hi)
    {
        var (ink, card, hairline) = (Brush("Brush.Ink"), Brush("Brush.Surface1"), Brush("Brush.Hairline"));
        foreach (var (edge, _) in _built)
        {
            var lit = Highlighted.Contains(edge.Key) || string.Equals(edge.Key, Hovered, StringComparison.Ordinal);
            using (Dimmed.Contains(edge.Key) ? context.PushOpacity(DimmedOpacity) : default)
            {
                if (edge.Arrow)
                {
                    var corners = HeadFor(edge);
                    var head = new StreamGeometry();
                    using (var ctx = head.Open())
                    {
                        ctx.BeginFigure(corners[0], isFilled: true);
                        ctx.LineTo(corners[1]);
                        ctx.LineTo(corners[2]);
                        ctx.EndFigure(isClosed: true);
                    }

                    context.DrawGeometry(lit ? hi : EdgeBrush(edge, wire), pen: null, head);
                }

                if (edge.Label is { } label)
                {
                    var text = LabelText(label, ink);
                    var pill = PillFor(edge, text);
                    context.DrawRectangle(card, new Pen(hairline, 1), pill, 4, 4);
                    context.DrawText(text, new Point(pill.X + 6, pill.Y + 2));
                }
            }
        }
    }

    // Every edge's stroke; returns the ports. A port is shared by every edge that ends on it: it is lit when any of them is, and
    // dimmed only when all of them are.
    private Dictionary<Point, (bool Lit, bool Dimmed)> DrawStrokes(DrawingContext context, IBrush wire, IBrush hi)
    {
        var ports = new Dictionary<Point, (bool Lit, bool Dimmed)>();
        foreach (var (edge, geometry) in _built)
        {
            var lit = Highlighted.Contains(edge.Key) || string.Equals(edge.Key, Hovered, StringComparison.Ordinal);
            var dim = Dimmed.Contains(edge.Key);
            using (dim ? context.PushOpacity(DimmedOpacity) : default)
            {
                context.DrawGeometry(brush: null, StrokePen(edge, lit ? hi : EdgeBrush(edge, wire), lit), geometry);
            }

            foreach (var end in new[] { edge.Route.Start.ToAvalonia(), edge.Route.End.ToAvalonia() })
            {
                ports[end] = ports.TryGetValue(end, out var seen) ? (seen.Lit || lit, seen.Dimmed && dim) : (lit, dim);
            }
        }

        return ports;
    }

    private IBrush EdgeBrush(MapEdge edge, IBrush wire) => edge.BrushKey is null ? wire : Brush(edge.BrushKey);

    private static Pen StrokePen(MapEdge edge, IBrush brush, bool lit) => new(brush, lit ? 2 * Stroke : Stroke, edge.Dashed ? new DashStyle(_dash, 0) : null, PenLineCap.Flat);

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Hovered = EdgeAt(e.GetPosition(this));
    }

    /// <inheritdoc/>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Hovered = null;
    }

    private void Rebuild()
    {
        _built = [.. Edges.Select(e => (e, e.Route.ToGeometry()))];
        InvalidateVisual();
    }

    private IBrush Brush(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}

#pragma warning restore MA0182
