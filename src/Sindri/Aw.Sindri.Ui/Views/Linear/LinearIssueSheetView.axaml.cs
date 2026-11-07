using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Linear;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.Views.Linear;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>
/// A Linear issue's sheet inside the shell's sheet: its buttons call the view model, ⌘S (Ctrl+S elsewhere) saves, and the focus the view model asks for lands on the title,
/// the conflict banner or Keep editing.
/// </summary>
internal sealed partial class LinearIssueSheetView : UserControl
{
    // ⌘ on macOS, Ctrl elsewhere, as the shell window reads it.
    private static readonly KeyModifiers _command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    private LinearIssueSheetViewModel? _sheet;

    public LinearIssueSheetView()
    {
        InitializeComponent();
    }

    private LinearIssueSheetViewModel? Sheet => DataContext as LinearIssueSheetViewModel;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        _sheet?.FocusRequested -= OnFocusRequested;
        _sheet = Sheet;
        _sheet?.FocusRequested += OnFocusRequested;

        base.OnDataContextChanged(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.S && e.KeyModifiers == _command && Sheet is { CanSave: true } sheet)
        {
            e.Handled = true;
            _ = sheet.Save(CancellationToken.None);
        }
    }

    private void OnFocusRequested(object? sender, ValueEventArgs<LinearIssueFocus> e) =>
        Dispatcher.UIThread.Post(() => Focus(e.Value), DispatcherPriority.Loaded);

    private void Focus(LinearIssueFocus where)
    {
        InputElement? target = where switch
        {
            LinearIssueFocus.Title => this.FindControl<TextBox>("IssueTitleBox"),
            LinearIssueFocus.Banner => this.FindControl<Border>("IssueConflict"),
            LinearIssueFocus.KeepEditing => this.FindControl<Button>("IssueKeepEditing"),
            _ => null,
        };
        _ = target?.Focus();
    }

    private void OnOpenInLinear(object? sender, RoutedEventArgs e) => _ = Sheet?.OpenInLinear(CancellationToken.None);

    private void OnOpenPr(object? sender, RoutedEventArgs e) => Sheet?.OpenPr();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = Sheet?.Save(CancellationToken.None);

    private void OnRetry(object? sender, RoutedEventArgs e) => _ = Sheet?.Retry(CancellationToken.None);

    private void OnConnect(object? sender, RoutedEventArgs e) => Sheet?.Connect();

    private void OnUseLinearsAll(object? sender, RoutedEventArgs e) => Sheet?.UseLinearsForAll();

    private void OnSaveMine(object? sender, RoutedEventArgs e) => _ = Sheet?.SaveMineAnyway(CancellationToken.None);

    private void OnKeepEditing(object? sender, RoutedEventArgs e) => Sheet?.KeepEditing();

    private void OnDiscard(object? sender, RoutedEventArgs e) => Sheet?.Discard();

    private void OnUseLinears(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: LinearConflictRowViewModel row })
        {
            row.UseLinears();
        }
    }
}
