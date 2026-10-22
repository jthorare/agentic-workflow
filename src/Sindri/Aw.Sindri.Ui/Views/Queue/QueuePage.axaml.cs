using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Queue;

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

    private void OnCreate(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Create.Open();

    private void OnShowHeld(object? sender, RoutedEventArgs e) => (DataContext as ShellViewModel)?.Queue.ShowHeld(ViewModels.Queue.QueueScope.Page);

    private void OnPanelEntered(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: true);

    private void OnPanelExited(object? sender, Avalonia.Input.PointerEventArgs e) => (DataContext as ShellViewModel)?.Queue.Hold(holding: false);
}
