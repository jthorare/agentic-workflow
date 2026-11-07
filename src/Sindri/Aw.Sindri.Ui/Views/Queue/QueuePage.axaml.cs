using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Queue;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Queue page; a row's title click opens its sheet, and Create item opens the form.</summary>
internal sealed partial class QueuePage : UserControl
{
    public QueuePage()
    {
        InitializeComponent();
        AddHandler(ItemRow.OpenEvent, OnOpen);
        AddHandler(ItemRow.SelectEvent, OnSelect);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageLayout.TierProperty && this.FindControl<Grid>("QueueHeader") is { } header)
        {
            PageLayout.Apply(header, change.GetNewValue<PageTier>());   // the same columns as the rows, so they line up
        }
    }

    // Focus arriving on a row, or on a control in it, selects it: the selection is the focus (§3.3).
    private void OnSelect(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.Queue.Select(id, ViewModels.Queue.QueueScope.Page);
        }
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (ItemRow.ItemIdOf(e) is { Length: > 0 } id && DataContext is ShellViewModel shell)
        {
            shell.Queue.Select(id, ViewModels.Queue.QueueScope.Page);
            shell.OpenItem(id);
        }
    }

    private void OnSegmentAll(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.Filter(ViewModels.Queue.QueueFilter.All);

    private void OnSegmentRunning(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.Filter(ViewModels.Queue.QueueFilter.Running);

    private void OnRepoChosen(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: string repo } && DataContext is ShellViewModel shell)
        {
            shell.Queue.ChooseRepo(repo);
        }
    }

    private void OnClosedToggle(object? sender, RoutedEventArgs e) => _ = (DataContext as ShellViewModel)?.ToggleClosed(CancellationToken.None);

    private void OnClearFilter(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.ClearFilter();

    private void OnSindriTab(object? sender, RoutedEventArgs e) => PickTab(linear: false, sender);

    private void OnLinearTab(object? sender, RoutedEventArgs e) => PickTab(linear: true, sender);

    // A toggle flips itself on a click; the tab shown is the view model's, so a click on the tab already on leaves it on.
    private void PickTab(bool linear, object? sender)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        shell.Linear.Choose(linear);
        if (sender is ToggleButton button)
        {
            button.IsChecked = linear ? shell.Linear.IsLinear : shell.Linear.IsSindri;
        }
    }

    // Left and Right move between the two tabs, Home and End to the first and the last; focus follows the pick.
    private void OnSwitchKey(object? sender, KeyEventArgs e)
    {
        var name = TabKeyName(e.Key);
        if (name is null || e.KeyModifiers != KeyModifiers.None || DataContext is not ShellViewModel shell || !shell.Linear.MoveTab(name))
        {
            return;
        }

        e.Handled = true;
        _ = this.FindControl<ToggleButton>(shell.Linear.IsLinear ? "WorkTabLinear" : "WorkTabSindri")?.Focus(NavigationMethod.Directional);
    }

    private static string? TabKeyName(Key key)
    {
        if (key is Key.Left or Key.Right or Key.Home or Key.End)
        {
            return key.ToString();
        }

        return null;
    }

    private void OnCreate(object? sender, RoutedEventArgs e)
    {
        OpenerNote.Raise(this, sender);
        (DataContext as ShellViewModel)?.Create.Open();
    }

    private void OnShowHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.ShowHeld(ViewModels.Queue.QueueScope.Page);

    private void OnPanelEntered(object? sender, PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: true);

    private void OnPanelExited(object? sender, PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: false);
}
#pragma warning restore MA0182
