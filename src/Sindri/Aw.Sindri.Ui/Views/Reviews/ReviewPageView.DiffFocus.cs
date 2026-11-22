using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.Views.Reviews;

// Keyboard focus across the diff's virtualization (#2776). The panel clears a row's content when the row scrolls out of the built range (the focused
// control leaves the tree and focus falls to nothing or to an ancestor), so the diff keys would stop reaching the page until a click. After a row is
// cleared or built, a posted check looks for that lost focus: the focused row takes it when it is built, else the row list takes it (the list sits
// inside the diff pane, so the keys keep working and the next key scrolls the row back).
internal sealed partial class ReviewPageView
{
    private bool _diffFocusCheckPosted;

    private static bool IsDiffRegion(ReviewFocusRegion region) => region is ReviewFocusRegion.DiffLine or ReviewFocusRegion.FileNote or ReviewFocusRegion.GroupHeader;

    private void OnDiffRowClearing(object? sender, ContainerClearingEventArgs e) => PostDiffFocusCheck();

    private void OnDiffRowPrepared(object? sender, ContainerPreparedEventArgs e) => PostDiffFocusCheck();

    private void PostDiffFocusCheck()
    {
        if (_diffFocusCheckPosted)
        {
            return;
        }

        _diffFocusCheckPosted = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _diffFocusCheckPosted = false;
                KeepDiffFocus();
            },
            DispatcherPriority.Loaded);
    }

    // Focus is lost when nothing holds it, or an ancestor of the list does. A click or Tab to somewhere else in the page is not lost.
    private bool IsDiffFocusLost(ItemsControl rows)
    {
        var held = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        return held is null || ReferenceEquals(held, rows) || (held is Visual visual && visual.IsVisualAncestorOf(rows));
    }

    private void KeepDiffFocus()
    {
        if (this.FindControl<ItemsControl>("ReviewDiffRows") is not { } rows || Page?.Focus is not { } focus || !IsDiffRegion(focus.Region) || !IsDiffFocusLost(rows))
        {
            return;
        }

        var built = rows.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible).ToList();
        _ = (Find(built, focus) ?? rows).Focus(NavigationMethod.Tab);
    }

    // Show swaps the deleted file's rows and hides the button, so a focused Show button would leave focus nowhere: the file's path row takes it instead.
    private void ShowFile(Control button, ReviewDiffFileViewModel file)
    {
        var hadFocus = button.IsFocused;
        file.Show();
        if (hadFocus)
        {
            Dispatcher.UIThread.Post(() => FocusFileRow(file), DispatcherPriority.Loaded);
        }
    }

    private void FocusFileRow(ReviewDiffFileViewModel file)
    {
        if (this.FindControl<ItemsControl>("ReviewDiffRows") is not { } rows)
        {
            return;
        }

        rows.UpdateLayout();
        var row = rows.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == "ReviewDiffFile" && ReferenceEquals(c.DataContext, file));
        _ = row?.Focus(NavigationMethod.Tab);
    }
}
