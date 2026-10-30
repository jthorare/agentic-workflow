using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>A project card's sheet inside the shell's sheet: its buttons call the view model; asking to remove puts focus on Keep, and keeping puts it back on Remove.</summary>
internal sealed partial class ProjectSheetView : UserControl
{
    private ProjectSheetViewModel? _watched;

    public ProjectSheetView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Watch();
    }

    private ProjectSheetViewModel? Sheet => DataContext as ProjectSheetViewModel;

    private void Watch()
    {
        if (_watched is { } old)
        {
            old.KeepFocusRequested -= OnKeepFocus;
            old.RemoveFocusRequested -= OnRemoveFocus;
        }

        _watched = Sheet;
        if (_watched is { } now)
        {
            now.KeepFocusRequested += OnKeepFocus;
            now.RemoveFocusRequested += OnRemoveFocus;
        }
    }

    // The question has just been drawn: focus goes to Keep once it is laid out.
    private void OnKeepFocus(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = this.FindControl<Button>("ProjectKeep")?.Focus(Avalonia.Input.NavigationMethod.Tab), DispatcherPriority.Loaded);

    // The question has closed: focus goes back to the Remove button that asked it, not to the hidden Keep.
    private void OnRemoveFocus(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => _ = this.FindControl<Button>("ProjectRemove")?.Focus(Avalonia.Input.NavigationMethod.Tab), DispatcherPriority.Loaded);

    private void OnOpenLink(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ProjectLinkItem link })
        {
            e.Handled = true;
            _ = link.Open(CancellationToken.None);
        }
    }

    private void OnEdit(object? sender, RoutedEventArgs e) => Sheet?.BeginEdit();

    private void OnCancelEdit(object? sender, RoutedEventArgs e) => Sheet?.CancelEdit();

    private void OnSave(object? sender, RoutedEventArgs e) => _ = Sheet?.Save(CancellationToken.None);

    private void OnSetState(object? sender, RoutedEventArgs e) => _ = Sheet?.SetState(CancellationToken.None);

    private void OnClearState(object? sender, RoutedEventArgs e) => _ = Sheet?.ClearState(CancellationToken.None);

    private void OnAskRemove(object? sender, RoutedEventArgs e) => Sheet?.AskRemove();

    private void OnKeep(object? sender, RoutedEventArgs e) => Sheet?.Keep();

    private void OnRemove(object? sender, RoutedEventArgs e) => _ = Sheet?.Remove(CancellationToken.None);

    private void OnHide(object? sender, RoutedEventArgs e) => _ = Sheet?.Hide(CancellationToken.None);

    private void OnUnhide(object? sender, RoutedEventArgs e) => _ = Sheet?.Unhide(CancellationToken.None);

    private void OnReload(object? sender, RoutedEventArgs e) => _ = Sheet?.Reload(CancellationToken.None);
}
#pragma warning restore MA0182
