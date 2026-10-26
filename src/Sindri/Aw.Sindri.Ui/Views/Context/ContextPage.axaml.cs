using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.Views.Context;

/// <summary>The Context page (U9a owns this class): the head, the control row and the stage.</summary>
internal sealed partial class ContextPage : UserControl
{
    private const double StageMinimum = 420;
    private const double PageEdge = 24;   // the room the page keeps under the stage
    private ContextPageViewModel? _page;
    private ScrollViewer? _scroller;

    public ContextPage()
    {
        InitializeComponent();
        ContextStack.Canvas.EmptyClicked += (_, _) => _page?.CloseInspector();   // a click on the empty canvas closes the inspector
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
        if (!e.Handled && e.Source is not TextBox && _page is { } page)
        {
            if (e.KeyModifiers == KeyModifiers.None)
            {
                e.Handled = Handle(page, e.Key);
            }
            else if (e.KeyModifiers == KeyModifiers.Alt && e.Key == Key.Left)
            {
                e.Handled = page.Up();   // Alt+Left goes up a layer, as Backspace does
            }
        }
    }

    // Esc closes the inspector; J and K walk the nodes; F and M choose the view (F3 spec 1 §5).
    private bool Handle(ContextPageViewModel page, Key key)
    {
        if (key == Key.Escape)
        {
            return page.Back();   // the inspector closes first, then the camera goes back to the whole map
        }

        if (key == Key.Back)
        {
            return page.Up();   // one layer up: the node, the query, the feature, the concept
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
            ContextStack.Canvas.Reveal(new Rect(node.X, node.Y, node.Width, node.Height), rightInset: 480);
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
        else if (e.Key == Key.Escape && DataContext is ContextPageViewModel back && back.Back())
        {
            e.Handled = true;   // the page's own handler skips keys typed in a text box, which is where focus sits after a query
        }
    }

    // The clear button: the box empties and the map is whole again.
    private void OnClearSearch(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContextPageViewModel page)
        {
            page.ClearSearch();
        }
    }

    // The back button on the failure row of a concept's map: up to the concepts.
    private void OnUp(object? sender, RoutedEventArgs e) => _page?.Up();

    // Retry on the failure row: the whole map is read again.
    private void OnRetryWhole(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContextPageViewModel page)
        {
            _ = page.ReadMapTop(CancellationToken.None);
        }
    }

    // View build job on the Building overlay: the Jobs page opens on that job.
    private void OnViewBuildJob(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ContextPageViewModel page)
        {
            page.RequestBuildJob();
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
        else if (_page is not null && string.Equals(e.PropertyName, nameof(ContextPageViewModel.AnchorText), StringComparison.Ordinal))
        {
            AnchorClear.IsVisible = _page.AnchorText.Length > 0;   // the clear button shows while the box holds something
        }
    }

    // The parts of the control row that take lists or controls the view model does not bind: the coverage strip.
    private void Sync(ContextPageViewModel page)
    {
        ContextStack.DataContext = page.Map;
        AnchorClear.IsVisible = page.AnchorText.Length > 0;
        if (page.Map is { } map)
        {
            ContextCoverageStrip.Areas = map.CoverageAreas;
            ContextCoverageStrip.Lead = map.CoverageLead;
            ContextCoverageStrip.Rest = map.CoverageRest;
        }

        Dispatch(FitStage);
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
