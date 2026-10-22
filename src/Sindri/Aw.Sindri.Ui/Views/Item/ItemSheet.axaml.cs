using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.Views.Controls;

namespace Aw.Sindri.Ui.Views.Item;

/// <summary>The item detail inside the shell's sheet; Load more asks for the timeline's next page.</summary>
internal sealed partial class ItemSheet : UserControl
{
    private ItemSheetViewModel? _sheet;

    public ItemSheet()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _sheet?.ControlsLeaving -= OnControlsLeaving;
        _sheet?.PropertyChanged -= OnSheetChanged;
        _sheet = DataContext as ItemSheetViewModel;
        _sheet?.ControlsLeaving += OnControlsLeaving;
        _sheet?.PropertyChanged += OnSheetChanged;
    }

    // The item is gone (404 on a refresh) and its body goes with it: focus inside the body moves to the sheet's title first.
    private void OnSheetChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(ItemSheetViewModel.IsNotFound), StringComparison.Ordinal) && _sheet is { IsNotFound: true }
            && TopLevel.GetTopLevel(this) is Window window && window.FocusManager?.GetFocusedElement() is Visual focused
            && this.FindControl<Control>("ItemBody") is { } body && body.IsVisualAncestorOf(focused))
        {
            _ = window.FindControl<Control>("SheetTitle")?.Focus(NavigationMethod.Tab);
        }
    }

    // L8: a control that holds the focus is about to go (its action is no longer allowed): focus moves to the sheet's title
    // first, so it is never left on nothing.
    private void OnControlsLeaving(object? sender, ControlsLeavingEventArgs leaving)
    {
        if (TopLevel.GetTopLevel(this) is not Window window || window.FocusManager?.GetFocusedElement() is not Visual focused)
        {
            return;
        }

        if (focused.FindAncestorOfType<CommandControl>(includeSelf: true)?.DataContext is ControlViewModel owner && leaving.Controls.Contains(owner))
        {
            _ = window.FindControl<Control>("SheetTitle")?.Focus(NavigationMethod.Tab);
        }
    }

    // The decision is answered in Needs you (F3 spec work §1 Q8): the link goes to that page.
    private void OnOpenNeedsYou(object? sender, RoutedEventArgs e) =>
        (TopLevel.GetTopLevel(this)?.DataContext as ViewModels.Shell.ShellViewModel)?.Navigate(Navigation.DeepLink.HomeNeedsYou);

    private void OnCopyId(object? sender, RoutedEventArgs e)
    {
        if (_sheet is { } sheet)
        {
            _ = sheet.CopyId();
        }
    }

    private void OnCopyDecide(object? sender, RoutedEventArgs e)
    {
        if (_sheet is { } sheet && sender is Button { DataContext: ItemDecisionRow row })
        {
            _ = sheet.Needs.CopyDecide(row.Id);
        }
    }

    // An option on one of the item's pending decisions: Decide, once; the buttons are rebuilt by the re-read, so focus
    // goes to the sheet's title when the pressed one is gone (focus is never lost).
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnDecisionOption(object? sender, RoutedEventArgs e)
#pragma warning restore MA0155
    {
        if (sender is not Button { DataContext: ViewModels.NeedsYou.DecisionOption option } button || _sheet is not { } sheet
            || button.FindAncestorOfType<ItemsControl>()?.FindAncestorOfType<StackPanel>()?.DataContext is not ItemDecisionRow row)
        {
            return;
        }

        await sheet.Needs.Choose(row.Id, option.Id).ConfigureAwait(true);
        if (TopLevel.GetTopLevel(this) is Window window && window.FocusManager?.GetFocusedElement() is not { IsEffectivelyEnabled: true })
        {
            _ = window.FindControl<Control>("SheetTitle")?.Focus(NavigationMethod.Tab);
        }
    }

    // Back (the 404 state) is Esc on the sheet: the shell closes it and moves focus to the opener or the page.
    private void OnBack(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this)?.DataContext as ViewModels.Shell.ShellViewModel)?.CloseSheet();

    private void OnLoadMore(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ItemSheetViewModel sheet)
        {
            _ = LoadMoreAndFocus(sheet);
        }
    }

    // L12: the page lands and the button may hide (the last page): focus goes to the first new row, never to nothing. A last page
    // with no row hides the button and adds none: focus goes to the timeline's last row. A page that didn't land moves nothing.
    private async Task LoadMoreAndFocus(ItemSheetViewModel sheet)
    {
        var before = sheet.Timeline.Count;
        var appended = await sheet.LoadMore(CancellationToken.None).ConfigureAwait(true);
        if (appended && sheet.Timeline.Count > before)
        {
            Dispatcher.UIThread.Post(() => FocusRow(before), DispatcherPriority.Loaded);
        }
        else if (appended && !sheet.HasMore)
        {
            Dispatcher.UIThread.Post(() => FocusRow(sheet.Timeline.Count - 1), DispatcherPriority.Loaded);
        }
    }

    private void FocusRow(int index)
    {
        var rows = this.FindControl<ItemsControl>("TimelineList")?.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("timeline-row")).ToList();
        if (rows is { } && index >= 0 && index < rows.Count)
        {
            _ = rows[index].Focus(NavigationMethod.Tab);
        }
    }
}
