using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The Workflows list: the column head and the rows under it. It measures its own width and picks the columns to keep (<see cref="ListColumns"/>),
/// and its rows scroll under the head when the page gives it a height (it takes the room the page's own scroller shows, less the head and toolbar).
/// </summary>
internal sealed partial class RunListView : UserControl
{
    // The page above the list: its head and toolbar, which the list does not scroll under.
    private const double PageChrome = 150;
    private const double MinListHeight = 240;

    private ScrollViewer? _page;

    public RunListView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _page = this.FindAncestorOfType<ScrollViewer>();
        _page?.PropertyChanged += OnPageChanged;
        Keep();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _page?.PropertyChanged -= OnPageChanged;
        _page = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Keep();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ListBands.ColumnsProperty && this.FindControl<Grid>("WorkflowColumnHeadGrid") is { } head)
        {
            ListBands.Apply(head, change.GetNewValue<ListColumns>());
        }
    }

    // The columns for the width the list has now, and the height its rows may scroll in.
    private void Keep()
    {
        if (Bounds.Width > 0)
        {
            ListBands.SetColumns(this, ListColumns.For(Bounds.Width));
        }

        if (_page is { Viewport.Height: > 0 } page && this.FindControl<ScrollViewer>("WorkflowsListScroll") is { } rows)
        {
            rows.MaxHeight = Math.Max(MinListHeight, page.Viewport.Height - PageChrome);
        }
    }

    private void OnPageChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ScrollViewer.ViewportProperty)
        {
            Keep();
        }
    }

    private void OnDoneToggle(object? sender, RoutedEventArgs e) => _ = (DataContext as WorkflowsViewModel)?.ToggleDone(CancellationToken.None);
}
