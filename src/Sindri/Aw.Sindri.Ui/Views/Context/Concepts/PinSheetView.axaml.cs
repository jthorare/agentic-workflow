using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Context.Concepts;

namespace Aw.Sindri.Ui.Views.Context.Concepts;

/// <summary>The Pin sheet (concept pins spec §3.6), bound to a <see cref="PinSheetViewModel"/>: focus starts on Name, a question puts focus on its safe button, and Esc answers the sheet from anywhere inside it.</summary>
#pragma warning disable MA0182 // why: ShellWindow.axaml places it through a data template, which the analyzer cannot see
internal sealed partial class PinSheetView : UserControl
{
    private PinSheetViewModel? _watched;

    /// <summary>Builds the sheet.</summary>
    public PinSheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    private PinSheetViewModel? Sheet => DataContext as PinSheetViewModel;

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
            old.NameFocusRequested -= OnNameFocus;
            old.KeepPinFocusRequested -= OnKeepPinFocus;
            old.UnpinFocusRequested -= OnUnpinFocus;
            old.KeepEditingFocusRequested -= OnKeepEditingFocus;
        }

        _watched = Sheet;
        if (_watched is { } now)
        {
            now.NameFocusRequested += OnNameFocus;
            now.KeepPinFocusRequested += OnKeepPinFocus;
            now.UnpinFocusRequested += OnUnpinFocus;
            now.KeepEditingFocusRequested += OnKeepEditingFocus;
            FocusLater("PinSheetName");
        }
    }

    private void OnNameFocus(object? sender, EventArgs e) => FocusLater("PinSheetName");

    private void OnKeepPinFocus(object? sender, EventArgs e) => FocusLater("PinSheetKeepPin");

    private void OnUnpinFocus(object? sender, EventArgs e) => FocusLater("Unpin");

    private void OnKeepEditingFocus(object? sender, EventArgs e) => FocusLater("PinSheetKeepEditing");

    private void FocusLater(string name) =>
        Dispatcher.UIThread.Post(() => _ = this.FindControl<Control>(name)?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);

    private void OnSubmit(object? sender, RoutedEventArgs e) => _ = Sheet?.Submit(CancellationToken.None);

    private void OnEsc(object? sender, RoutedEventArgs e) => Sheet?.Escape();

    private void OnAskUnpin(object? sender, RoutedEventArgs e) => Sheet?.AskUnpin();

    private void OnUnpinConfirm(object? sender, RoutedEventArgs e) => _ = Sheet?.Unpin(CancellationToken.None);

    private void OnKeepPin(object? sender, RoutedEventArgs e) => Sheet?.KeepPin();

    private void OnDiscard(object? sender, RoutedEventArgs e) => Sheet?.Discard();

    private void OnKeepEditing(object? sender, RoutedEventArgs e) => Sheet?.KeepEditing();

    private void OnAddWord(object? sender, RoutedEventArgs e) => Sheet?.AddWord();

    private void OnTryAgain(object? sender, RoutedEventArgs e) => _ = Sheet?.TryAgain(CancellationToken.None);

    private void OnAddFeature(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: PinFeatureRow row })
        {
            Sheet?.AddFeature(row);
        }
    }

    private void OnNewWordKey(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Sheet?.AddWord();
            e.Handled = true;
        }
    }
}
#pragma warning restore MA0182
