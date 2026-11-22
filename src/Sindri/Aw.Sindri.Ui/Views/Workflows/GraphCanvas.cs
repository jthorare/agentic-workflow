using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The read-only workflow graph (#2258 PR 17 part 2), drawn in one <see cref="Render"/> pass: a card per node, a bezier arrow per edge with an arrow head and
/// the branch output ("true", "false") at its midpoint. Drag on empty space pans, Ctrl+wheel zooms (0.5 to 2), the arrow keys and Tab move the selection and
/// Enter raises <see cref="WorkflowGraphViewModel.NodeSelected"/> for it. The selected card has a ring; a node's status is a glyph and a word, never colour alone.
/// </summary>
internal sealed partial class GraphCanvas : Control
{
    /// <summary>The smallest zoom.</summary>
    internal const double MinZoom = 0.5;

    /// <summary>The largest zoom.</summary>
    internal const double MaxZoom = 2;

    private static readonly Point _defaultPan = new(24, 24);

    /// <summary>The graph drawn.</summary>
    internal static readonly StyledProperty<WorkflowGraphViewModel?> GraphProperty = AvaloniaProperty.Register<GraphCanvas, WorkflowGraphViewModel?>(nameof(Graph));

    /// <summary>The zoom scale, clamped to 0.5 to 2.</summary>
    internal static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<GraphCanvas, double>(nameof(Zoom), 1, coerce: (_, v) => Math.Clamp(v, MinZoom, MaxZoom));

    /// <summary>Where the world's origin sits in this control.</summary>
    internal static readonly StyledProperty<Point> PanProperty = AvaloniaProperty.Register<GraphCanvas, Point>(nameof(Pan), _defaultPan, coerce: static (o, v) => o is GraphCanvas canvas ? canvas.HeldPan(v) : v);

    /// <summary>The text size setting as a factor (1 = 100 %): cards are laid out at it, and a change wraps them again.</summary>
    internal static readonly StyledProperty<double> TextScaleProperty = AvaloniaProperty.Register<GraphCanvas, double>(nameof(TextScale), 1);

    private const double ViewMargin = 24;
    private const double MinHandle = 40;
    private const double ZoomStep = 1.1;
    private const double WheelStep = 48;

    private WorkflowGraphViewModel? _watched;
    private Point? _panFrom;
    private Point _panStart;
    private double _laidOutWidth;
    private double _laidOutScale = 1;
    private WorkflowGraphViewModel? _laidOut;
    private bool _fitPending;   // a workflow just opened and the person has not moved the view: fit it to the canvas

    static GraphCanvas()
    {
        AffectsRender<GraphCanvas>(GraphProperty, ZoomProperty, PanProperty, TextScaleProperty);
        FocusableProperty.OverrideDefaultValue<GraphCanvas>(defaultValue: true);
        ClipToBoundsProperty.OverrideDefaultValue<GraphCanvas>(defaultValue: true);
        _ = ZoomProperty.Changed.AddClassHandler<GraphCanvas>((canvas, _) =>
        {
            canvas.CoerceValue(PanProperty);   // a new zoom changes the scroll extent
            canvas.ViewChanged?.Invoke(canvas, EventArgs.Empty);
        });
        _ = PanProperty.Changed.AddClassHandler<GraphCanvas>((canvas, _) => canvas.ViewChanged?.Invoke(canvas, EventArgs.Empty));
        _ = TextScaleProperty.Changed.AddClassHandler<GraphCanvas>((canvas, _) => canvas.FitIfPending());
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

    /// <summary>Gets or sets the text size setting as a factor.</summary>
    internal double TextScale
    {
        get => GetValue(TextScaleProperty);
        set => SetValue(TextScaleProperty, value);
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

    /// <summary>How many times the canvas has drawn: a test reads it to see that a theme switch redrew.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="GraphCanvas"/> class.</summary>
    public GraphCanvas()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();   // the brushes are looked up while drawing, so a theme switch needs a new pass
        SizeChanged += (_, _) =>
        {
            FitIfPending();
            ViewChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    /// <inheritdoc/>
    protected override AutomationPeer OnCreateAutomationPeer() => new GraphCanvasAutomationPeer(this);

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
            _fitPending = false;
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
            e.Pointer.Capture(control: null);
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        e.Handled = Wheel(e.Delta, e.KeyModifiers, e.GetPosition(this));
    }

    /// <summary>One wheel step. Ctrl+wheel zooms about the pointer. A plain wheel scrolls up and down; a sideways swipe (or Shift+wheel) scrolls left and right. The canvas takes the
    /// wheel only while it can move that way; at its end, or when it fits, the wheel is left to the page.</summary>
    /// <param name="delta">The wheel's delta.</param>
    /// <param name="modifiers">The keys held.</param>
    /// <param name="at">The pointer, in this control.</param>
    /// <returns>True when the canvas took the wheel.</returns>
    internal bool Wheel(Vector delta, KeyModifiers modifiers, Point at)
    {
        _fitPending = false;
        if (!modifiers.HasFlag(KeyModifiers.Control))
        {
            var (dx, dy) = modifiers.HasFlag(KeyModifiers.Shift) && Math.Abs(delta.X) < 1e-9 ? (delta.Y, 0d) : (delta.X, delta.Y);
            var before = Pan;
            Pan = new Point(Pan.X + (dx * WheelStep), Pan.Y + (dy * WheelStep));
            return Pan != before;
        }

        // Zoom about the pointer: the world point under it stays under it.
        var world = ToWorld(at);
        Zoom *= Math.Pow(ZoomStep, delta.Y);
        Pan = new Point(at.X - (world.X * Zoom), at.Y - (world.Y * Zoom));
        return true;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Graph is not { } graph)
        {
            return;
        }

#pragma warning disable IDE0072 // every other key is not the canvas's
        e.Handled = e.Key switch
        {
            Key.F when e.KeyModifiers == KeyModifiers.None => FitKey(),
            Key.Right or Key.Down => graph.SelectNext(),
            Key.Left or Key.Up => graph.SelectPrevious(),
            Key.Tab => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? graph.SelectPrevious() : graph.SelectNext(),   // at either end Tab is not handled, so focus moves on
            Key.Enter => graph.ActivateSelected(),
            Key.Delete => graph.RequestRemoveSelected(),   // a request only: the edit bar asks before anything is sent
            _ => false,
        };
#pragma warning restore IDE0072
        if (e.Handled && graph.SelectedId is { } id)
        {
            ScrollIntoView(id);
        }
    }

    private bool FitKey()
    {
        FitNow();
        return true;
    }

    private void OnGraphChanged(WorkflowGraphViewModel? old, WorkflowGraphViewModel? graph)
    {
        old?.PropertyChanged -= OnGraphProperty;

        // A live refresh reads the same workflow again and keeps the view; another workflow (or one after none) starts at the corner, unzoomed.
        if (old is null || graph is null || !string.Equals(old.Slug, graph.Slug, StringComparison.Ordinal))
        {
            Zoom = 1;
            Pan = _defaultPan;
            _fitPending = true;
        }

        _watched = graph;
        graph?.PropertyChanged += OnGraphProperty;

        AutomationProperties.SetName(this, graph?.Name ?? string.Empty);
        (ControlAutomationPeer.FromElement(this) as GraphCanvasAutomationPeer)?.Refresh();
        FitIfPending();
        InvalidateVisual();
    }

    private void OnGraphProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _watched))
        {
            if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.SelectedId), StringComparison.Ordinal))
            {
                (ControlAutomationPeer.FromElement(this) as GraphCanvasAutomationPeer)?.SelectionChanged(_watched?.SelectedId);
            }

            InvalidateVisual();
        }
    }
}
