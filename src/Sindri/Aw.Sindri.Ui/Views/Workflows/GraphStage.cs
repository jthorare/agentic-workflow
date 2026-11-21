using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    private readonly GraphCanvas _canvas = new();
    private readonly MiniMapControl _map = new() { Width = GraphZoom.MapWidth, Height = GraphZoom.MapHeight, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, GraphZoom.MapMargin, GraphZoom.MapMargin), IsVisible = false };
    private readonly TextBlock _percent = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 48, TextAlignment = TextAlignment.Center };
    private readonly Border _strip = new() { Height = GraphZoom.StripHeight };

    static GraphStage()
    {
        _ = GraphProperty.Changed.AddClassHandler<GraphStage>((s, e) => s._canvas.Graph = e.NewValue as WorkflowGraphViewModel);
        _ = TextScaleProperty.Changed.AddClassHandler<GraphStage>((s, e) => s._canvas.TextScale = e.NewValue is double d ? d : 1);
    }

    /// <summary>Initializes a new instance of the <see cref="GraphStage"/> class.</summary>
    public GraphStage()
    {
        RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(new GridLength(GraphZoom.StripHeight))];
        var top = new Grid();
        top.Children.Add(_canvas);
        top.Children.Add(_map);
        SetRow(top, 0);
        top.ClipToBounds = true;
        Children.Add(top);

        var fit = MakeButton("Fit", "Fit", () => _canvas.FitNow());
        var minus = MakeButton("−", "Zoom out", () => _canvas.ZoomOut());
        var plus = MakeButton("+", "Zoom in", () => _canvas.ZoomIn());
        ToolTip.SetTip(fit, "Fit  F");
        _strip.Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(24, 0), VerticalAlignment = VerticalAlignment.Center, Children = { fit, minus, _percent, plus } };
        SetRow(_strip, 1);
        Children.Add(_strip);

        _canvas.ViewChanged += (_, _) => Refresh();
        _map.Centred += (_, e) => _canvas.CentreOn(e.Value);
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
    internal GraphCanvas Canvas => _canvas;

    /// <summary>Gets the minimap.</summary>
    internal MiniMapControl Map => _map;

    /// <summary>Gets the band under the canvas.</summary>
    internal Border Strip => _strip;

    /// <summary>Gets the zoom percent's words.</summary>
    internal string PercentText => _percent.Text ?? string.Empty;

    private static Button MakeButton(string text, string name, Action click)
    {
        var button = new Button { Content = text, MinWidth = 36 };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => click();
        return button;
    }

    private void Refresh()
    {
        _percent.Text = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{_canvas.ZoomPercent}%");
        var scrolls = _canvas.Scrolls;
        _map.IsVisible = scrolls;
        if (scrolls && Graph is { } graph)
        {
            var fill = this.TryFindResource("Brush.InkSubtle", ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;
            _map.Nodes = [.. graph.Nodes.Select(n => new MiniMapNode(new Rect(n.X, n.Y, n.Width, n.Height), fill, n.IsSelected))];
            _map.World = _canvas.World;
            _map.Viewport = MiniMapControl.VisiblePart(_canvas.Viewport, _canvas.World);
        }
    }
}
