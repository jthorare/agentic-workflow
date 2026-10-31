using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Context.Whole;

namespace Aw.Sindri.Ui.Views.Context.Whole;

/// <summary>The page sheet (pages spec §3.6), bound to a <see cref="PageSheetViewModel"/>: focus starts on Name, a question puts focus on its safe button, and Esc answers the sheet from anywhere inside it.</summary>
#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
internal sealed partial class PageSheetView : UserControl
{
    private PageSheetViewModel? _watched;

    /// <summary>Builds the sheet.</summary>
    public PageSheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    private PageSheetViewModel? Sheet => DataContext as PageSheetViewModel;

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && Sheet is { } sheet)
        {
            e.Handled = sheet.Escape();   // a text box in the sheet is where Esc is typed; the page's own handler skips those
        }
    }

    private void Watch()
    {
        if (_watched is { } old)
        {
            old.CancelRemoveFocusRequested -= OnCancelRemoveFocus;
            old.RemovePageFocusRequested -= OnRemovePageFocus;
            old.KeepEditingFocusRequested -= OnKeepEditingFocus;
            old.NameFocusRequested -= OnNameFocus;
            old.PropertyChanged -= OnSheetChanged;
        }

        _watched = Sheet;
        if (_watched is { } now)
        {
            now.CancelRemoveFocusRequested += OnCancelRemoveFocus;
            now.RemovePageFocusRequested += OnRemovePageFocus;
            now.KeepEditingFocusRequested += OnKeepEditingFocus;
            now.NameFocusRequested += OnNameFocus;
            now.PropertyChanged += OnSheetChanged;
            FocusLater("PageSheetName");
        }
    }

    // The fields appear once the page is read: focus starts on Name then.
    private void OnSheetChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(PageSheetViewModel.IsReady), StringComparison.Ordinal) && Sheet is { IsReady: true })
        {
            FocusLater("PageSheetName");
        }
    }

    private void OnCancelRemoveFocus(object? sender, EventArgs e) => FocusLater("PageSheetRemoveCancel");

    private void OnRemovePageFocus(object? sender, EventArgs e) => FocusLater("PageSheetRemovePage");

    private void OnKeepEditingFocus(object? sender, EventArgs e) => FocusLater("PageSheetKeepEditing");

    private void OnNameFocus(object? sender, EventArgs e) => FocusLater("PageSheetName");

    private void FocusLater(string name) =>
        Dispatcher.UIThread.Post(() => _ = this.FindControl<Control>(name)?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    private void OnClose(object? sender, RoutedEventArgs e) => Sheet?.Escape();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = Sheet?.Save(CancellationToken.None);

    private void OnAddAction(object? sender, RoutedEventArgs e) => Sheet?.AddAction();

    private void OnRemoveAction(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PageSheetActionViewModel row })
        {
            Sheet?.RemoveAction(row);
        }
    }

    private void OnAskRemove(object? sender, RoutedEventArgs e) => Sheet?.AskRemove();

    private void OnRemoveConfirm(object? sender, RoutedEventArgs e) => _ = Sheet?.Remove(CancellationToken.None);

    private void OnRemoveCancel(object? sender, RoutedEventArgs e) => Sheet?.CancelRemove();

    private void OnDiscard(object? sender, RoutedEventArgs e) => Sheet?.Close();

    private void OnKeepEditing(object? sender, RoutedEventArgs e) => Sheet?.KeepEditing();
}
#pragma warning restore MA0182
