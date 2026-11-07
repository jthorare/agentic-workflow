using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Linear;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.Views.Linear;

#pragma warning disable MA0182 // why: QueuePage.axaml places it, which the analyzer cannot see
/// <summary>The Work page's Linear view: its buttons call the view model, and "Show more" moves focus to the first new row.</summary>
internal sealed partial class LinearWorkView : UserControl
{
    private LinearWorkViewModel? _subscribed;

    public LinearWorkView()
    {
        InitializeComponent();
    }

    private LinearWorkViewModel? Work => DataContext as LinearWorkViewModel;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != DataContextProperty)
        {
            return;
        }

        _subscribed?.FocusRequested -= OnFocusRequested;
        _subscribed = Work;
        _subscribed?.FocusRequested += OnFocusRequested;
    }

    private void OnFocusRequested(object? sender, FocusRequestedEventArgs e) =>
        Dispatcher.UIThread.Post(() => FocusRow(e.ItemId), DispatcherPriority.Loaded);

    // The row's title button takes focus: it is the row's first control.
    private void FocusRow(string key) =>
        this.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => string.Equals(b.Name, "LinearRowTitle", StringComparison.Ordinal) && b.DataContext is LinearIssueRowViewModel row && string.Equals(row.Key, key, StringComparison.Ordinal))
            ?.Focus();

    private void OnDismiss(object? sender, RoutedEventArgs e) => _ = Work?.Dismiss();

    private void OnRetryStrip(object? sender, RoutedEventArgs e) => _ = Work?.RetryStrip();

    private void OnOpenStripRow(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LinearSinceRowViewModel row })
        {
            Work?.RequestIssue(row.Key);
        }
    }

    private void OnOpenIssue(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LinearIssueRowViewModel row })
        {
            Work?.RequestIssue(row.Key);
        }
    }

    private void OnConnect(object? sender, RoutedEventArgs e) => Work?.RequestConnect();

    private void OnClearFilters(object? sender, RoutedEventArgs e) => Work?.ClearFilters();

    private void OnShowMore(object? sender, RoutedEventArgs e) => _ = Work?.ShowMore();
}
#pragma warning restore MA0182
