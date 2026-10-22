using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Views.Context;

/// <summary>The Context page (U9a owns this class): the head, the control row and the stage.</summary>
internal sealed partial class ContextPage : UserControl
{
    private const double StageMinimum = 420;
    private const double PageEdge = 24;   // the room the page keeps under the stage
    private readonly StackPanel _startingRows = new() { MinWidth = 320 };
    private ContextPageViewModel? _page;
    private ScrollViewer? _scroller;

    public ContextPage()
    {
        InitializeComponent();
        ContextStack.Canvas.EmptyClicked += (_, _) => _page?.CloseInspector();   // a click on the empty canvas closes the inspector
        StartingPoints.Flyout = new Flyout { Content = _startingRows, Placement = PlacementMode.BottomEdgeAlignedLeft };   // a closed flyout's content is not in the tree, so it is built here
    }

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _page?.PropertyChanged -= OnPageChanged;
        _page = DataContext as ContextPageViewModel;
        if (_page is not null)
        {
            _page.PropertyChanged += OnPageChanged;
            Sync(_page);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        _scroller?.PropertyChanged += OnScrollerChanged;
        ContextStack.DataContext = _page?.Map;
        FitStage();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _scroller?.PropertyChanged -= OnScrollerChanged;
        _scroller = null;

        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Source is not TextBox && e.KeyModifiers == KeyModifiers.None && _page is { } page)
        {
            e.Handled = Handle(page, e.Key);
        }
    }

    // Esc closes the inspector; J and K walk the nodes; F and M choose the view (F3 spec 1 §5).
    private bool Handle(ContextPageViewModel page, Key key)
    {
        if (key == Key.Escape)
        {
            if (!page.InspectorOpen)
            {
                return false;
            }

            page.CloseInspector();
            return true;
        }

        if (key is Key.F or Key.M)
        {
            page.ChooseView(files: key == Key.F);
            return true;
        }

        if (key is Key.J or Key.K && page.MapVisible)
        {
            _ = RevealAfter(key == Key.J ? page.SelectNext() : page.SelectPrevious());
            return true;
        }

        return false;
    }

    // The canvas pans the chosen node out from under the inspector (480 px over the right edge).
    private async Task RevealAfter(Task choose)
    {
        await choose.ConfigureAwait(true);
        if (_page?.Map is { SelectedKey: { } key } map && map.Node(key) is { } node)
        {
            ContextStack.Canvas.Reveal(new Avalonia.Rect(node.X, node.Y, node.Width, node.Height), rightInset: 480);
        }
    }

    // Enter in the anchor box reads the lenses for what is typed.
    private void OnAnchorKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ContextPageViewModel page)
        {
            e.Handled = true;
            _ = page.Submit();
        }
    }

    private void OnPick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string anchor } && DataContext is ContextPageViewModel page)
        {
            _ = page.Pick(anchor);
        }
    }

    private void OnPageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_page is not null && string.Equals(e.PropertyName, nameof(ContextPageViewModel.Map), StringComparison.Ordinal))
        {
            Sync(_page);
        }
    }

    // The parts of the control row that take lists or controls the view model does not bind: the coverage strip and the starting points.
    private void Sync(ContextPageViewModel page)
    {
        ContextStack.DataContext = page.Map;
        if (page.Map is { } map)
        {
            ContextCoverageStrip.Areas = map.CoverageAreas;
            ContextCoverageStrip.Lead = map.CoverageLead;
            ContextCoverageStrip.Rest = map.CoverageRest;
            StartingPointsText.Text = map.StartingPoints;
            AutomationProperties.SetName(StartingPoints, map.StartingPoints);
            _startingRows.Children.Clear();
            foreach (var anchor in map.Anchors)
            {
                _startingRows.Children.Add(Row(anchor));
            }
        }

        Dispatch(FitStage);
    }

    private static Grid Row(MapAnchorViewModel anchor)
    {
        var tick = new Icons.StateIcon { Icon = "Ok", VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = anchor.Label, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
        label.Classes.Add("body");
        var kind = new TextBlock { Text = anchor.KindWord.ToLowerInvariant(), VerticalAlignment = VerticalAlignment.Center };
        kind.Classes.Add("caption");
        kind.Classes.Add("subtle");
        var row = new Grid { Height = 36, ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        row.Children.Add(tick);
        row.Children.Add(label);
        row.Children.Add(kind);
        Grid.SetColumn(label, 1);
        Grid.SetColumn(kind, 2);
        AutomationProperties.SetName(row, anchor.Label + ", " + anchor.KindWord.ToLowerInvariant());
        return row;
    }

    private static void Dispatch(Action action) => Avalonia.Threading.Dispatcher.UIThread.Post(action);

    private void OnScrollerChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            FitStage();
        }
    }

    // The page scrolls, so the stage cannot fill what is left; it is given the viewport's height under its own top.
    private void FitStage()
    {
        if (_scroller is not { } scroller || scroller.Viewport.Height <= 0)
        {
            return;
        }

        var top = ContextStageHost.TranslatePoint(default, this)?.Y ?? 0;
        ContextStageHost.Height = Math.Max(StageMinimum, scroller.Viewport.Height - top - PageEdge);
    }
}
