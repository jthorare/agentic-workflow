using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The whole map's stage (whole-map spec §3): the canvas, the mini-map from the areas, the zoom pill and the list of features. Bound to a <see cref="WholeMapViewModel"/>.</summary>
internal sealed partial class WholeMapView : UserControl
{
    private const double PanStep = 0.1;
    private WholeMapViewModel? _map;
    /// <summary>The strip along the stage's bottom edge that the mini-map floats over: its 112 high box, the 14 px margin under it and 14 over it.</summary>
    internal const double MiniMapBand = 140;

    private bool _fitted;

    /// <summary>Initializes a new instance of the <see cref="WholeMapView"/> class.</summary>
    public WholeMapView()
    {
        InitializeComponent();
        WholeCanvas.FitBottomInset = MiniMapBand;
        AutomationProperties.SetName(WholeZoomOut, Labels.ContextMapZoomOut);
        AutomationProperties.SetName(WholeZoomIn, Labels.ContextMapZoomIn);
        AutomationProperties.SetName(WholeZoomFit, Labels.ContextMapZoomFit);
        WholeCanvas.PropertyChanged += (_, e) =>
        {
            if (e.Property == ZoomBorder.ViewProperty)
            {
                ViewChanged();
            }
        };
        WholeCanvas.SizeChanged += (_, _) => Arrange();
        WholeMiniMap.Centred += (_, e) => Centre(e.Value);
        WholeCanvas.Clicked += (_, e) => WholeWorld.Click(e.GetPosition(WholeWorld));
        ViewChanged();
    }

    /// <summary>Gets the pannable canvas.</summary>
    internal ZoomBorder Canvas => WholeCanvas;

    /// <summary>Gets the world the canvas holds.</summary>
    internal WholeMapCanvas World => WholeWorld;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_map is { } old)
        {
            old.ZoomRequested -= OnZoomRequested;
            old.FitRequested -= OnFitRequested;
            old.PropertyChanged -= OnMapChanged;
            old.EmphasisChanged -= OnEmphasisChanged;
            old.CentreRequested -= OnCentreRequested;
        }

        _map = DataContext as WholeMapViewModel;
        WholeWorld.Map = _map;
        if (_map is not null)
        {
            _map.ZoomRequested += OnZoomRequested;
            _map.FitRequested += OnFitRequested;
            _map.PropertyChanged += OnMapChanged;
            _map.EmphasisChanged += OnEmphasisChanged;
            _map.CentreRequested += OnCentreRequested;
            _fitted = false;
            Arrange();
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Source is not TextBox && WholeCanvas.HandleZoomKey(e.Key))
        {
            e.Handled = true;
        }
    }

    // A new answer: the world and its zoom range change, and the first one is fitted (a later one keeps the person's view).
    private void OnMapChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WholeMapViewModel.Head), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(WholeMapViewModel.IsAreaOpen), StringComparison.Ordinal))
        {
            _fitted = false;
            Arrange();
        }

        if (string.Equals(e.PropertyName, nameof(WholeMapViewModel.Shown), StringComparison.Ordinal))
        {
            Arrange();   // a search filtered the map, or ended: the minimap and the zoom floor follow the set that is drawn
        }

        WholeWorld.InvalidateVisual();   // the answer's nodes standing out is not a property the canvas watches
    }

    private void OnEmphasisChanged(object? sender, EventArgs e) => WholeWorld.Highlight(_map?.Focus, _map?.Emphasis);

    private void OnCentreRequested(object? sender, ValueEventArgs<WholeRect> e) => Centre(new Point(e.Value.CenterX, e.Value.CenterY));

    private void OnNodePanelClose(object? sender, RoutedEventArgs e) => _ = _map?.Panel.Close();

    private void OnNodePanelOpenSource(object? sender, RoutedEventArgs e) => _ = _map?.Panel.OpenSource();

    private void OnNodePanelEdge(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: NodeEdgeRowViewModel row })
        {
            row.Jump();
        }
    }

    private void OnFitRequested(object? sender, EventArgs e) => WholeCanvas.Fit();

    private void OnZoomRequested(object? sender, ValueEventArgs<WholeRect> e) =>
        WholeCanvas.FitBounds(new Rect(e.Value.X - 24, e.Value.Y - 24, e.Value.Width + 48, e.Value.Height + 48));

    // The world, the zoom floor that shows all of it, the mini-map's cards; and, once the stage has a size, the first open.
    private void Arrange()
    {
        if (_map?.Geometry is not { } geometry)
        {
            return;
        }

        var box = _map.FitBox ?? geometry.World;
        var world = new Rect(box.X, box.Y, box.Width, box.Height);
        WholeCanvas.WorldBounds = world;
        WholeMiniMap.World = world;
        WholeMiniMap.Nodes = [.. MiniMapRects(_map.Shown ?? geometry).Select(card => new MiniMapNode(card.Rect, Brush(card.Colour), Selected: false))];
        WholeWorld.InvalidateMeasure();
        var stage = WholeCanvas.FitStage;   // less the strip the mini-map floats over, so no card opens or fits under it
        if (stage.Width <= 0 || stage.Height <= 0 || world.Width <= 0 || world.Height <= 0)
        {
            return;
        }

        var fit = Math.Min(stage.Width / world.Width, stage.Height / world.Height);
        WholeCanvas.MinZoom = Math.Clamp(fit, 0.02, WholeMapViewModel.MaxScale);
        WholeCanvas.MaxZoom = WholeMapViewModel.MaxScale;
        if (!_fitted)
        {
            _fitted = true;
            WholeCanvas.SetView(ZoomView.Fit(stage, world, new ZoomRange(WholeMapViewModel.FirstOpenFloor, WholeMapViewModel.MaxScale)));
        }

        ViewChanged();
    }

    // What the mini-map draws: a card per feature, whichever level the answer is at.
    private static IEnumerable<(Rect Rect, int Colour)> MiniMapRects(WholeMapGeometry geometry) => geometry.ByArea
        ? geometry.Tiles.Select(t => (new Rect(t.Rect.X, t.Rect.Y, t.Rect.Width, t.Rect.Height), WholePalette.For(t.AreaIndex).Colour))
        : geometry.Columns.Select(c => (new Rect(c.Rect.X, c.Rect.Y, c.Rect.Width, c.Rect.Height), WholePalette.For(c.AreaIndex).Colour));

    // The zoom, the part of the world on screen for the canvas and the mini-map, and the percentage.
    private void ViewChanged()
    {
        var view = WholeCanvas.View;
        var size = WholeCanvas.Bounds.Size;
        var viewport = view.Scale > 0 ? new Rect(-view.X / view.Scale, -view.Y / view.Scale, size.Width / view.Scale, size.Height / view.Scale) : default;
        WholeWorld.Scale = view.Scale;
        WholeWorld.Viewport = viewport;
        WholeMiniMap.Viewport = viewport;
        WholeZoomValue.Text = Labels.ContextMapZoomPct(view.Scale);
    }

    // The mini-map asks for a point of the world in the middle of the stage.
    private void Centre(Point world)
    {
        var view = WholeCanvas.View;
        var size = WholeCanvas.Bounds.Size;
        WholeCanvas.MoveToView(view with { X = (size.Width / 2) - (world.X * view.Scale), Y = (size.Height / 2) - (world.Y * view.Scale) });
    }

    private void OnMiniMapKey(object? sender, KeyEventArgs e)
    {
        var view = WholeCanvas.View;
        var size = WholeCanvas.Bounds.Size;
        var (dx, dy) = (0.0, 0.0);
        if (e.Key == Key.Left)
        {
            dx = PanStep * size.Width;
        }
        else if (e.Key == Key.Right)
        {
            dx = -PanStep * size.Width;
        }
        else if (e.Key == Key.Up)
        {
            dy = PanStep * size.Height;
        }
        else if (e.Key == Key.Down)
        {
            dy = -PanStep * size.Height;
        }

        if (dx != 0 || dy != 0)
        {
            WholeCanvas.MoveToView(view with { X = view.X + dx, Y = view.Y + dy });
            e.Handled = true;
        }
    }

    private void OnCrumb(object? sender, ValueEventArgs<Crumb> e) => _map?.Navigate(e.Value);

    private void OnBandButton(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: BandButton button })
        {
            _map?.DrillInto(button.Band);
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e) => _map?.GoUp();

    private void OnZoomIn(object? sender, RoutedEventArgs e) => WholeCanvas.ZoomIn();

    private void OnZoomOut(object? sender, RoutedEventArgs e) => WholeCanvas.ZoomOut();

    private void OnZoomFit(object? sender, RoutedEventArgs e) => WholeCanvas.Fit();

    private IBrush Brush(int colour) => this.TryFindResource(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Brush.Area{colour + 1}"), ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
