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
    internal static readonly StyledProperty<Point> PanProperty = AvaloniaProperty.Register<GraphCanvas, Point>(nameof(Pan), _defaultPan);

    private const double ViewMargin = 24;
    private const double MinHandle = 40;
    private const double ZoomStep = 1.1;

    private WorkflowGraphViewModel? _watched;
    private Point? _panFrom;
    private Point _panStart;
    private bool _fitPending;   // a workflow just opened and the person has not moved the view: fit it to the canvas

    static GraphCanvas()
    {
        AffectsRender<GraphCanvas>(GraphProperty, ZoomProperty, PanProperty);
        FocusableProperty.OverrideDefaultValue<GraphCanvas>(defaultValue: true);
        ClipToBoundsProperty.OverrideDefaultValue<GraphCanvas>(defaultValue: true);
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

    /// <summary>How many times the canvas has drawn: a test reads it to see that a theme switch redrew.</summary>
    internal int RenderCount { get; private set; }

    /// <summary>Initializes a new instance of the <see cref="GraphCanvas"/> class.</summary>
    public GraphCanvas()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();   // the brushes are looked up while drawing, so a theme switch needs a new pass
        SizeChanged += (_, _) => FitIfPending();
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
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        // Zoom about the pointer: the world point under it stays under it.
        _fitPending = false;
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

#pragma warning disable IDE0072 // every other key is not the canvas's
        e.Handled = e.Key switch
        {
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

    /// <summary>Pans the least that brings a node's card fully into view, with a margin; does nothing when it is already inside.</summary>
    /// <param name="id">The node's id.</param>
    internal void ScrollIntoView(string id)
    {
        if (NodeRect(id) is not { } card || (new Rect(Bounds.Size).Contains(card.TopLeft) && new Rect(Bounds.Size).Contains(card.BottomRight)))
        {
            return;
        }

        static double Shift(double near, double far, double size) =>
            near < 0 || far - near > size ? ViewMargin - near : far > size ? size - ViewMargin - far : 0;

        Pan = new Point(Pan.X + Shift(card.Left, card.Right, Bounds.Width), Pan.Y + Shift(card.Top, card.Bottom, Bounds.Height));
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

    // A graph wider or taller than the canvas is shrunk (never below the minimum zoom) so every card shows; one that fits keeps the default view (#2668).
    private void FitIfPending()
    {
        if (!_fitPending || Graph is not { Nodes.Count: > 0 } graph || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var world = graph.Nodes.Select(WorldRect).Aggregate(static (a, b) => a.Union(b));
        var room = new Size(Math.Max(1, Bounds.Width - (2 * ViewMargin)), Math.Max(1, Bounds.Height - (2 * ViewMargin)));
        var zoom = Math.Min(1, Math.Min(room.Width / world.Width, room.Height / world.Height));
        if (zoom >= 1)
        {
            Zoom = 1;
            Pan = _defaultPan;
            return;
        }

        Zoom = zoom;
        Pan = new Point(ViewMargin - (world.X * Zoom), ViewMargin - (world.Y * Zoom));
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
