using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // until the Workflows page places the control

/// <summary>
/// The read-only workflow graph (#2258 PR 17 part 2), drawn in one <see cref="Render"/> pass: a card per node, a bezier arrow per edge with an arrow head and
/// the branch output ("true", "false") at its midpoint. Drag on empty space pans, Ctrl+wheel zooms (0.5 to 2), the arrow keys and Tab move the selection and
/// Enter raises <see cref="WorkflowGraphViewModel.NodeSelected"/> for it. The selected card has a ring; a node's status is a glyph and a word, never colour alone.
/// </summary>
internal sealed class GraphCanvas : Control
{
    /// <summary>The smallest zoom.</summary>
    internal const double MinZoom = 0.5;

    /// <summary>The largest zoom.</summary>
    internal const double MaxZoom = 2;

    /// <summary>The graph drawn.</summary>
    internal static readonly StyledProperty<WorkflowGraphViewModel?> GraphProperty = AvaloniaProperty.Register<GraphCanvas, WorkflowGraphViewModel?>(nameof(Graph));

    /// <summary>The zoom scale, clamped to 0.5 to 2.</summary>
    internal static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<GraphCanvas, double>(nameof(Zoom), 1, coerce: (_, v) => Math.Clamp(v, MinZoom, MaxZoom));

    /// <summary>Where the world's origin sits in this control.</summary>
    internal static readonly StyledProperty<Point> PanProperty = AvaloniaProperty.Register<GraphCanvas, Point>(nameof(Pan), new Point(24, 24));

    private const double ArrowSize = 9;
    private const double MinHandle = 40;
    private const double ZoomStep = 1.1;
    private const double Radius = 8;
    private const double RingGap = 3;

    private WorkflowGraphViewModel? _watched;
    private Point? _panFrom;
    private Point _panStart;

    static GraphCanvas()
    {
        AffectsRender<GraphCanvas>(GraphProperty, ZoomProperty, PanProperty);
        FocusableProperty.OverrideDefaultValue<GraphCanvas>(true);
        ClipToBoundsProperty.OverrideDefaultValue<GraphCanvas>(true);
        _ = GraphProperty.Changed.AddClassHandler<GraphCanvas>((c, e) => c.OnGraphChanged(e.OldValue as WorkflowGraphViewModel, e.NewValue as WorkflowGraphViewModel));
    }

    /// <summary>Gets or sets the graph drawn.</summary>
    internal WorkflowGraphViewModel? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <summary>Gets or sets the zoom scale.</summary>
    internal double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>Gets or sets where the world's origin sits in this control.</summary>
    internal Point Pan
    {
        get => GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    /// <summary>A node's card in this control's coordinates, after pan and zoom.</summary>
    /// <param name="id">The node's id.</param>
    /// <returns>The rectangle, or null when the graph has no such node.</returns>
    internal Rect? NodeRect(string id) =>
        Graph?.Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal)) is { } node ? ToScreen(WorldRect(node)) : null;

    /// <summary>Where an edge's branch label sits (the middle of its curve), in this control's coordinates.</summary>
    /// <param name="edge">The edge.</param>
    /// <returns>The point.</returns>
    internal Point LabelCenter(GraphEdgeViewModel edge)
    {
        var (start, c1, c2, end) = Curve(edge);
        return ToScreen(new Point(
            (start.X + (3 * c1.X) + (3 * c2.X) + end.X) / 8,
            (start.Y + (3 * c1.Y) + (3 * c2.Y) + end.Y) / 8));
    }

    /// <summary>The id of the topmost node under a point.</summary>
    /// <param name="point">A point in this control.</param>
    /// <returns>The id, or null for empty space.</returns>
    internal string? NodeAt(Point point)
    {
        var world = ToWorld(point);
        return Graph?.Nodes.LastOrDefault(n => WorldRect(n).Contains(world))?.Id;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));   // so a drag on empty space hits this control
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

            foreach (var node in graph.Nodes)
            {
                DrawNode(context, node);
            }
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 320 : availableSize.Height);

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = Focus();
        var at = e.GetPosition(this);
        if (NodeAt(at) is { } id)
        {
            _ = Graph?.SelectNode(id);
        }
        else
        {
            _panFrom = at;
            _panStart = Pan;
            e.Pointer.Capture(this);
        }

        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_panFrom is { } from)
        {
            var at = e.GetPosition(this);
            Pan = new Point(_panStart.X + (at.X - from.X), _panStart.Y + (at.Y - from.Y));
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panFrom is not null)
        {
            _panFrom = null;
            e.Pointer.Capture(null);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        // Zoom about the pointer: the world point under it stays under it.
        var at = e.GetPosition(this);
        var world = ToWorld(at);
        Zoom *= Math.Pow(ZoomStep, e.Delta.Y);
        Pan = new Point(at.X - (world.X * Zoom), at.Y - (world.Y * Zoom));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Graph is not { } graph)
        {
            return;
        }

        e.Handled = e.Key switch
        {
            Key.Right or Key.Down => graph.SelectNext(),
            Key.Left or Key.Up => graph.SelectPrevious(),
            Key.Tab => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? graph.SelectPrevious() : graph.SelectNext(),   // at either end Tab is not handled, so focus moves on
            Key.Enter => graph.ActivateSelected(),
            _ => false,
        };
    }

    private void OnGraphChanged(WorkflowGraphViewModel? old, WorkflowGraphViewModel? graph)
    {
        if (old is not null)
        {
            old.PropertyChanged -= OnGraphProperty;
        }

        _watched = graph;
        if (graph is not null)
        {
            graph.PropertyChanged += OnGraphProperty;
        }

        AutomationProperties.SetName(this, graph?.Name ?? string.Empty);
        InvalidateVisual();
    }

    private void OnGraphProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _watched))
        {
            InvalidateVisual();
        }
    }

    private void DrawEdge(DrawingContext context, GraphEdgeViewModel edge)
    {
        var (start, c1, c2, end) = Curve(edge);
        var wire = Brush("Brush.Wire");
        using (var curve = new StreamGeometry())
        {
            using (var ctx = curve.Open())
            {
                ctx.BeginFigure(start, false);
                ctx.CubicBezierTo(c1, c2, end);
                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(wire, 2), curve);
        }

        // The arrow head points along the curve's last stretch (from the second control point to the end).
        var angle = Math.Atan2(end.Y - c2.Y, end.X - c2.X);
        Point Wing(double turn) => new(end.X - (ArrowSize * Math.Cos(angle + turn)), end.Y - (ArrowSize * Math.Sin(angle + turn)));
        using var head = new StreamGeometry();
        using (var ctx = head.Open())
        {
            ctx.BeginFigure(end, true);
            ctx.LineTo(Wing(0.45));
            ctx.LineTo(Wing(-0.45));
            ctx.EndFigure(true);
        }

        context.DrawGeometry(wire, null, head);

        if (edge.Label is { } label)
        {
            var text = Text(label, 11, Brush("Brush.Ink"), 80, semiBold: true);
            var mid = new Point((start.X + (3 * c1.X) + (3 * c2.X) + end.X) / 8, (start.Y + (3 * c1.Y) + (3 * c2.Y) + end.Y) / 8);
            var pill = new Rect(mid.X - (text.Width / 2) - 6, mid.Y - (text.Height / 2) - 2, text.Width + 12, text.Height + 4);
            context.DrawRectangle(Brush("Brush.Surface1"), new Pen(Brush("Brush.Hairline"), 1), pill, 4, 4);
            context.DrawText(text, new Point(pill.X + 6, pill.Y + 2));
        }
    }

    private void DrawNode(DrawingContext context, GraphNodeViewModel node)
    {
        var rect = WorldRect(node);
        if (node.IsSelected)
        {
            context.DrawRectangle(null, new Pen(Brush("Brush.Primary"), 2), rect.Inflate(RingGap), Radius + RingGap, Radius + RingGap);
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

    private static string ToneKey(GraphTone tone) => tone switch
    {
        GraphTone.Success => "Brush.Success",
        GraphTone.Error => "Brush.Error",
        GraphTone.Attention => "Brush.Attention",
        _ => "Brush.InkSubtle",
    };

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

#pragma warning restore MA0182
