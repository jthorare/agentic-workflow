using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Views.Reviews;

/// <summary>The fix-task sheet's content: focus goes to its text box when it opens and to the outcome text after a send.</summary>
internal sealed partial class ReviewFixSheetView : UserControl
{
    private ReviewFixSheetViewModel? _watched;

    public ReviewFixSheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Unwatch();
        base.OnDetachedFromVisualTree(e);
    }

    // One subscription at a time: the old view model's is dropped when the DataContext changes and when the view leaves the tree.
    private void Watch()
    {
        Unwatch();
        if (Sheet is { } sheet)
        {
            sheet.PropertyChanged += OnSheetChanged;
            _watched = sheet;
            if (sheet.IsOpen)
            {
                Dispatcher.UIThread.Post(MoveFocus, DispatcherPriority.Loaded);   // the first open builds this view after Open set Focus: that change came before the handler
            }
        }
    }

    private void Unwatch()
    {
        if (_watched is { } old)
        {
            old.PropertyChanged -= OnSheetChanged;
            _watched = null;
        }
    }

    private void OnSheetChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ReviewFixSheetViewModel.Focus), StringComparison.Ordinal))
        {
            Dispatcher.UIThread.Post(MoveFocus, DispatcherPriority.Loaded);
        }
    }

    private ReviewFixSheetViewModel? Sheet => DataContext as ReviewFixSheetViewModel;

    private void MoveFocus()
    {
        Control? target = Sheet?.Focus.Region switch
        {
            ReviewFocusRegion.FixText => this.FindControl<TextBox>("ReviewFixText"),
            ReviewFocusRegion.FixOutcome => this.FindControl<TextBlock>("ReviewFixOutcome"),
            ReviewFocusRegion.None or ReviewFocusRegion.StateHeading or ReviewFocusRegion.StripFirst or ReviewFocusRegion.Panel or ReviewFocusRegion.DiffLine or ReviewFocusRegion.GroupHeader or ReviewFocusRegion.FileNote => null,
            _ => null,
        };
        _ = target?.Focus(NavigationMethod.Tab);
    }

    private void OnSend(object? sender, RoutedEventArgs e) => _ = Sheet?.Send(CancellationToken.None);

    private void OnRetry(object? sender, RoutedEventArgs e) => _ = Sheet?.Retry(CancellationToken.None);

    private void OnClose(object? sender, RoutedEventArgs e) => Sheet?.Close();
}
