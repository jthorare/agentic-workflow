using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The workflow canvas with its zoom strip and minimap (#3684): the canvas above, a 56 px band under it with Fit, minus, the percent and plus (a band, never an overlay), and the
/// minimap bottom right inside the canvas, shown only while the canvas scrolls.
/// </summary>
internal sealed class GraphStage : Grid
{
    /// <summary>The graph drawn.</summary>
    internal static readonly StyledProperty<WorkflowGraphViewModel?> GraphProperty = AvaloniaProperty.Register<GraphStage, WorkflowGraphViewModel?>(nameof(Graph));

    /// <summary>The text size setting as a factor.</summary>
    internal static readonly StyledProperty<double> TextScaleProperty = AvaloniaProperty.Register<GraphStage, double>(nameof(TextScale), 1);

    private readonly TextBlock _percent = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 48, TextAlignment = TextAlignment.Center };

    static GraphStage()
    {
        _ = GraphProperty.Changed.AddClassHandler<GraphStage>((s, e) =>
        {
            (e.OldValue as WorkflowGraphViewModel)?.PropertyChanged -= s.OnGraphProperty;
            (e.NewValue as WorkflowGraphViewModel)?.PropertyChanged += s.OnGraphProperty;
            s.Canvas.Graph = e.NewValue as WorkflowGraphViewModel;
        });
        _ = TextScaleProperty.Changed.AddClassHandler<GraphStage>((s, e) => s.Canvas.TextScale = e.NewValue is double d ? d : 1);
    }

    /// <summary>Initializes a new instance of the <see cref="GraphStage"/> class.</summary>
    public GraphStage()
    {
        RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(new GridLength(GraphZoom.StripHeight))];
        var top = new Grid();
        top.Children.Add(Canvas);
        MapPanel.Child = Map;
        ActualThemeVariantChanged += (_, _) => PaintMapPanel();
        top.Children.Add(MapPanel);
        SetRow(top, 0);
        top.ClipToBounds = true;
        Children.Add(top);

        var fit = MakeButton(Labels.WfZoomFit, Labels.WfZoomFit, Canvas.FitNow);
        var minus = MakeButton(Labels.WfZoomMinus, Labels.ContextMapZoomOut, Canvas.ZoomOut);   // one word for both zoom bands: the context map's
        var plus = MakeButton(Labels.WfZoomPlus, Labels.ContextMapZoomIn, Canvas.ZoomIn);
        (fit.Name, minus.Name, _percent.Name, plus.Name) = ("GraphZoomFit", "GraphZoomOut", "GraphZoomPercent", "GraphZoomIn");   // set in code, so not in the page's name scope: two stages can share them
        ToolTip.SetTip(fit, Labels.WfZoomFitTip);
        Strip.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(24, 0), VerticalAlignment = VerticalAlignment.Center, Children = { fit, minus, _percent, plus } };
        SetRow(Strip, 1);
        Children.Add(Strip);

        Canvas.ViewChanged += (_, _) => Refresh();
        Map.Centred += (_, e) => Canvas.CentreOn(e.Value);
    }

    /// <summary>Gets or sets the graph drawn.</summary>
    internal WorkflowGraphViewModel? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    /// <summary>Gets or sets the text size setting as a factor.</summary>
    internal double TextScale
    {
        get => GetValue(TextScaleProperty);
        set => SetValue(TextScaleProperty, value);
    }

    /// <summary>Gets the canvas.</summary>
    internal GraphCanvas Canvas { get; } = new();

    /// <summary>Gets the minimap.</summary>
    internal MiniMapControl Map { get; } = new() { IsVisible = false };

    /// <summary>Gets the opaque panel the minimap sits on, in the canvas's bottom right corner (#4055): shown only while the canvas scrolls, like the map.</summary>
    internal Border MapPanel { get; } = new()
    {
        Width = GraphZoom.MapWidth,
        Height = GraphZoom.MapHeight,
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(0, 0, GraphZoom.MapMargin, GraphZoom.MapMargin),
        Padding = new Thickness(8),
        BorderThickness = new Thickness(1),
        IsVisible = false,
    };

    /// <summary>Gets the band under the canvas.</summary>
    internal Border Strip { get; } = new() { Height = GraphZoom.StripHeight };

    /// <summary>Gets the zoom percent's words.</summary>
    internal string PercentText => _percent.Text ?? string.Empty;

    // The minimap's panel: the cards' surface (opaque, so nothing under the map shows through, #4055), a hairline border and the small radius.
    private void PaintMapPanel()
    {
        MapPanel.Background = this.TryFindResource("Brush.Surface2", ActualThemeVariant, out var surface) && surface is IBrush fill ? fill : Brushes.Black;
        MapPanel.BorderBrush = this.TryFindResource("Brush.Hairline", ActualThemeVariant, out var line) && line is IBrush hairline ? hairline : Brushes.Gray;
        MapPanel.CornerRadius = this.TryFindResource("Radius.SM", ActualThemeVariant, out var radius) && radius is CornerRadius corner ? corner : new CornerRadius(6);
    }

    private static Button MakeButton(string text, string name, Action click)
    {
        var button = new Button { Content = text, MinWidth = 36 };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    // The minimap outlines the selected card, so a selection change redraws it.
    private void OnGraphProperty(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(WorkflowGraphViewModel.SelectedId), StringComparison.Ordinal))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        _percent.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Canvas.ZoomPercent}%");
        var scrolls = Canvas.Scrolls;
        Map.IsVisible = scrolls;
        PaintMapPanel();
        MapPanel.IsVisible = scrolls;   // no reserved room and no panel while the graph fits: ExtentHeight adds the map's 144 px only while it shows
        if (scrolls && Graph is { } graph)
        {
            var fill = this.TryFindResource("Brush.InkSubtle", ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
            Map.Nodes = [.. graph.Nodes.Select(n => new MiniMapNode(new Rect(n.X, n.Y, n.Width, n.Height), fill, n.IsSelected))];
            Map.World = Canvas.World;
            Map.Viewport = MiniMapControl.VisiblePart(Canvas.Viewport, Canvas.World);
        }
    }
}
