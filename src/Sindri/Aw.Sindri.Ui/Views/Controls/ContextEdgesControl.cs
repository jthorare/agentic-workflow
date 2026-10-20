using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182 // until slice 2 places the control in the context page

/// <summary>
/// The layer that draws the context map's edges (F3 spec 1 §2, "Edges") in one Render pass: a 2 px stroke in Wire, dashed
/// 8 4 when suggested, a 10 px port with a 3 px radius at each end, WireHi for edges around the hovered or selected node, and
/// a 12 px wide hit stroke so a thin line is easy to hover. Edges it is told are dimmed draw at 42 %.
/// </summary>
internal sealed class ContextEdgesControl : Control, ICustomHitTest
{
    /// <summary>The edges to draw.</summary>
    internal static readonly StyledProperty<IReadOnlyList<MapEdge>> EdgesProperty =
        AvaloniaProperty.Register<ContextEdgesControl, IReadOnlyList<MapEdge>>(nameof(Edges), []);

    /// <summary>The keys of the edges drawn in WireHi.</summary>
    internal static readonly StyledProperty<IReadOnlySet<string>> HighlightedProperty =
        AvaloniaProperty.Register<ContextEdgesControl, IReadOnlySet<string>>(nameof(Highlighted), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The keys of the edges drawn dimmed.</summary>
    internal static readonly StyledProperty<IReadOnlySet<string>> DimmedProperty =
        AvaloniaProperty.Register<ContextEdgesControl, IReadOnlySet<string>>(nameof(Dimmed), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>The key of the edge under the pointer, or null.</summary>
    internal static readonly StyledProperty<string?> HoveredProperty = AvaloniaProperty.Register<ContextEdgesControl, string?>(nameof(Hovered));

    private const double Stroke = 2;
    private const double HitWidth = 12;
    private const double PortSize = 10;   // outer, the 1 px outline included: the rectangle itself is 9 so the outline lands on whole pixels
    private const double PortRadius = 3;
    private const double DimmedOpacity = 0.42;

    private static readonly double[] _dash = [4, 2];   // Pen dashes count in stroke widths: 8 4 at 2 px

    private IReadOnlyList<(MapEdge Edge, StreamGeometry Geometry)> _built = [];

    static ContextEdgesControl()
    {
        AffectsRender<ContextEdgesControl>(HighlightedProperty, DimmedProperty, HoveredProperty);
        _ = EdgesProperty.Changed.AddClassHandler<ContextEdgesControl>((c, _) => c.Rebuild());
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
            if (_built[i].Edge.Route.DistanceTo(point) <= HitWidth / 2)
            {
                return _built[i].Edge.Key;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool HitTest(Point point) => EdgeAt(point) is not null;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var (wire, hi, card) = (Brush("Brush.Wire"), Brush("Brush.WireHi"), Brush("Brush.Surface1"));
        var ports = DrawStrokes(context, wire, hi);

        // The ports go on after every stroke, so no later edge paints over one.
        foreach (var (at, (lit, dim)) in ports)
        {
            using (dim ? context.PushOpacity(DimmedOpacity) : default)
            {
                var half = (PortSize - 1) / 2;
                context.DrawRectangle(card, new Pen(lit ? hi : wire, 1), new Rect(at.X - half, at.Y - half, PortSize - 1, PortSize - 1), PortRadius, PortRadius);
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
                var pen = new Pen(lit ? hi : wire, Stroke, edge.Dashed ? new DashStyle(_dash, 0) : null, PenLineCap.Flat);
                context.DrawGeometry(brush: null, pen, geometry);
            }

            foreach (var end in new[] { edge.Route.Start, edge.Route.End })
            {
                ports[end] = ports.TryGetValue(end, out var seen) ? (seen.Lit || lit, seen.Dimmed && dim) : (lit, dim);
            }
        }

        return ports;
    }

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
