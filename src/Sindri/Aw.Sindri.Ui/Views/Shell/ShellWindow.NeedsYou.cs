using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.NeedsYou;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.NeedsYou;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus on Home's Needs-you rows (<see cref="ShellWindow"/>, continued; UI spec §11.2): the selection is the focus, and focus is never lost.</summary>
internal sealed partial class ShellWindow
{
    private string? _needsYouSelected;

    private void ListenNeedsYou(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.NeedsYou.FocusRequested += OnNeedsYouFocus;
            shell.NeedsYou.PropertyChanged += OnNeedsYouChanged;
            shell.NeedsYou.Seen.PropertyChanged += OnSeenChanged;
        }
        else
        {
            shell.NeedsYou.FocusRequested -= OnNeedsYouFocus;
            shell.NeedsYou.PropertyChanged -= OnNeedsYouChanged;
            shell.NeedsYou.Seen.PropertyChanged -= OnSeenChanged;
        }
    }

    // Esc closed a row's sheet: focus goes back to the row it was opened from.
    // When the sheet was opened from a strip row (Answer), that row is the one it goes back to.
    private void OnNeedsYouFocus(object? sender, NeedsYouRowEventArgs request)
    {
        var strip = Shell?.OpenerStrip;
        Dispatcher.UIThread.Post(() => (strip is null ? (Action)(() => FocusNeedsYouRow(request.RowId)) : () => FocusStripRow(strip))(), DispatcherPriority.Loaded);
    }

    // J / K (or the row after a cleared one) moved the selection: the row's frame takes focus, on the keyboard's ring. A refresh that
    // cleared the selected row moves the selection on its own: focus follows only if it was in that row, or is lost by now, never
    // when the user is in the search field, the top bar or Mark all seen (§3.3, §5).
    private void OnNeedsYouChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(NeedsYouViewModel.SelectedId), StringComparison.Ordinal))
        {
            return;
        }

        // No row left to select (the last one cleared): focus still goes somewhere (the page region).
        var id = Shell?.NeedsYou.SelectedId;
        var follows = FocusFollowsSelection(id);
        Dispatcher.UIThread.Post(() =>
        {
            if (follows || !HasUsableFocus)
            {
                (id is null ? KeepFocus : (Action)(() => FocusNeedsYouRow(id)))();
            }
        }, DispatcherPriority.Loaded);
    }

    // Read when the selection changes: focus follows when the user moved it, or when it is lost already (the cleared row was removed
    // under it), or was still in the row the selection left. Focus anywhere else stays where it is.
    private bool FocusFollowsSelection(string? id)
    {
        var leftBehind = _needsYouSelected;
        _needsYouSelected = id;
        return Shell?.NeedsYou.SelectionMovedOnItsOwn != true || FocusManager?.GetFocusedElement() is not Visual focused || IsInRow(focused, leftBehind);
    }

    private static bool IsInRow(Visual focused, string? rowId) =>
        focused.GetSelfAndVisualAncestors().OfType<NeedsYouRow>().Any(r => string.Equals(r.RowId, rowId, StringComparison.Ordinal));

    // The Undo offer ends (pressed, or ten seconds): a focus on its button moves on.
    private void OnSeenChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(NeedsYouSeenViewModel.UndoOffered), StringComparison.Ordinal) && Shell?.NeedsYou.Seen.UndoOffered == false)
        {
            Dispatcher.UIThread.Post(KeepFocus, DispatcherPriority.Loaded);
        }
    }

    // Esc closes the sheet over the page first, then the inbox (back to Home).
    private static bool EscapeSheetOrInbox(ShellViewModel shell)
    {
        if (shell.IsSheetShown)
        {
            shell.CloseSheet();
            return true;
        }

        return shell.LeaveNeedsYouInbox();
    }

    // The inbox opened or closed: focus goes to the selected row, on the inbox or back on Home, so it is never lost.
    private void FollowInbox()
    {
        var selected = Shell?.NeedsYou.SelectedId;
        Dispatcher.UIThread.Post(() => (selected is null ? KeepFocus : (Action)(() => FocusNeedsYouRow(selected)))(), DispatcherPriority.Loaded);
    }

    private void FocusNeedsYouRow(string rowId)
    {
        if (Shell?.IsNeedsYouInbox == true)
        {
            if (!this.Get<NeedsYouPage>("NeedsYouInboxPage").FocusRow(rowId))
            {
                KeepFocus();   // the row is not drawn: focus still goes somewhere
            }

            return;
        }

        var row = this.Get<Border>("Page").GetVisualDescendants().OfType<NeedsYouRow>()
            .FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.RowId, rowId, StringComparison.Ordinal));
        if (row?.IsKeyboardFocusWithin == true)
        {
            return;   // focus is already in the row (a click on its title or a control): it stays where it is
        }

        var frame = Usable(row?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal)));
        if (frame is null)
        {
            KeepFocus();   // the row is not drawn, or the sheet covers the page and its frame can't take focus: focus still goes somewhere
            return;
        }

        _ = frame.Focus(NavigationMethod.Tab);
    }
}
