using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Views.Item;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the page's single keys (UI spec §8), passed to the shell view model.</summary>
internal sealed partial class ShellWindow
{
    // §8: single keys never fire while typing in a field, over a layer, or with a modifier other than Shift ('.').
    private bool PageKey(KeyEventArgs e)
    {
        if (Shell is not { } shell || KeysBlocked(shell, e))
        {
            return false;
        }

        // Shift+A marks every Needs-you row seen, from any page.
        if (e is { Key: Key.A, KeyModifiers: KeyModifiers.Shift })
        {
            shell.MarkAllSeen();
            return true;
        }

        if (KeyName(e) is not { } key)
        {
            return false;
        }

        if (key == "Enter" && EnterIntoSheet(shell))
        {
            return true;
        }

        if (!shell.Press(key, inSheet: FocusInSheet()))
        {
            return false;
        }

        FocusOpenedField(shell, key);
        return true;
    }

    private static bool KeysBlocked(ViewModels.Shell.ShellViewModel shell, KeyEventArgs e) =>
        shell is not { IsChromeVisible: true, IsNavigationEnabled: true } || e.Source is TextBox
        || shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen || shell.Start.IsOpen || shell.ApproveAll.IsOpen
        || (e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None;

    // §11.2: with the sheet beside the page and open on the selected row's item (the row drawn on this page), Enter again moves into its first action
    // (its heading when it has none). Covering the page, focus is already in the sheet.
    private bool EnterIntoSheet(ViewModels.Shell.ShellViewModel shell)
    {
        if (!shell.IsSheetShown || Covering || FocusInSheet() || shell.Queue.IsGuarded || !shell.IsSelectedSheetOpen)
        {
            return false;
        }

        if (!FocusFirstSheetAction())
        {
            _ = this.Get<Control>("SheetTitle").Focus(NavigationMethod.Tab);   // no action, or none takes focus (pending): the heading
        }

        return true;
    }

    // The item sheet's first control, or the decision form's first usable tab stop in tree order (its note while enabled, else the first
    // option: the note sits above the options, so entering at an option would leave it behind forward Tab); the instance frame's first read
    // action (Open in, else Copy status).
    private bool FocusFirstSheetAction()
    {
        if (Shell is { IsInstanceSheet: true } shell)
        {
            shell.Instance.FocusInitial();
            return true;
        }

        var descendants = SheetPanel.GetVisualDescendants().ToList();
        return descendants.OfType<Controls.CommandControl>().FirstOrDefault(c => c.IsEffectivelyVisible)?.FocusRun() == true
            || descendants.OfType<InputElement>().FirstOrDefault(IsDecisionFormStop)?.Focus(NavigationMethod.Tab) == true;
    }

    private static bool IsDecisionFormStop(InputElement c) =>
        c.IsEffectivelyVisible && c.IsEffectivelyEnabled && c.Focusable && c.IsTabStop
        && (c is Button b ? b.Classes.Contains("decision-option") : c is TextBox note && string.Equals(note.Name, "DecisionNote", StringComparison.Ordinal));

    // §11.2 "Enter again (or Tab)": Tab from the last control of the selected row whose item the sheet shows (beside the page)
    // goes into the sheet. Tab within the row is unchanged. This handler runs before the focus manager's, which ignores a handled key.
    private bool TabOutOfOpenRow(KeyEventArgs e)
    {
        if (e is not { Key: Key.Tab, KeyModifiers: KeyModifiers.None } || Shell is not { IsSelectedSheetOpen: true } shell || AnyLayerOpen(shell)
            || FocusManager?.GetFocusedElement() is not Visual focused || OpenRowFrame(focused) is not { } frame)
        {
            return false;
        }

        // The last tab stop of the row, found by the row's own order: the focus manager's next element is the nearest one on screen, which
        // can be outside the row while the row's Decline is still ahead.
        return IsLastStopOf(frame, focused) && EnterIntoSheet(shell);
    }

    private static bool IsLastStopOf(Border frame, Visual focused) =>
        frame.GetVisualDescendants().OfType<InputElement>().LastOrDefault(c => c.Focusable && c.IsTabStop && c.IsEffectivelyVisible && c.IsEffectivelyEnabled) is not { } last || ReferenceEquals(last, focused);

    private static bool AnyLayerOpen(ViewModels.Shell.ShellViewModel shell) => shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen || shell.Start.IsOpen || shell.ApproveAll.IsOpen;

    // The frame of the row that the focused control is in, when that row is the one the sheet shows.
    private Border? OpenRowFrame(Visual focused) =>
        focused.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "RowFrame", StringComparison.Ordinal) || string.Equals(b.Name, "WorkflowRowFrame", StringComparison.Ordinal))
            is { } frame && IsOpenRow(frame.DataContext) ? frame : null;

    private bool IsOpenRow(object? row) => row switch
    {
        ViewModels.Workflows.InstanceRowViewModel workflow => Shell is { IsSelectedWorkflowOpen: true } && string.Equals(workflow.Address, Shell.Instance.Address, StringComparison.Ordinal),
        ViewModels.Queue.ItemRowViewModel item => string.Equals(item.Id, Shell?.Item.ItemId, StringComparison.Ordinal),
        ViewModels.NeedsYou.NeedsYouRowViewModel need => string.Equals(need.Id, Shell?.NeedsYou.SheetRowId, StringComparison.Ordinal),
        _ => false,
    };

    // §8, §11.2: keys belong to the focused region; the row keys are the page's, not the sheet's.
    private bool FocusInSheet() => FocusManager?.GetFocusedElement() is Visual focused && (SheetPanel == focused || SheetPanel.IsVisualAncestorOf(focused));

    // A key that opened a field moves focus into it: C the create form's title, / the Queue page's filter.
    private void FocusOpenedField(ViewModels.Shell.ShellViewModel shell, string key)
    {
        if (shell.Create.IsOpen)
        {
            Dispatcher.UIThread.Post(() => Built<CreateItemDialog>("CreateItemDialog")?.FocusTitle(), DispatcherPriority.Loaded);
        }
        else if (key == "/" && shell.Queue.IsFiltering)
        {
            Dispatcher.UIThread.Post(() => _ = this.Get<TextBox>("QueueFilter").Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        }
    }

    // The key as the view model names it: a letter (no Shift), "Enter" or ".".
    private static string? KeyName(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            return "Enter";
        }

        if (e.Key == Key.OemPeriod)
        {
            return ".";
        }

        if (e.Key == Key.OemQuestion && e.KeyModifiers == KeyModifiers.None)
        {
            return "/";
        }

        return e.Key is >= Key.A and <= Key.Z && e.KeyModifiers == KeyModifiers.None ? e.Key.ToString() : null;
    }

    // The forms and dialogs over the shell: the approve-all dialog, the Start form, then the create form.
    private static bool EscapeForm(ViewModels.Shell.ShellViewModel shell)
    {
        if (shell.ApproveAll.IsOpen)
        {
            shell.ApproveAll.Close();
            return true;
        }

        if (shell.Start.IsOpen)
        {
            shell.Start.Close();
            return true;
        }

        if (!shell.Create.IsOpen)
        {
            return false;
        }

        shell.Create.Close();
        return true;
    }

    // Esc's U2 layers, above the sheet (§8 "the topmost layer"): the Start form, the create form, then a destructive control's question.
    private bool EscapeItemLayer(ViewModels.Shell.ShellViewModel shell)
    {
        if (EscapeForm(shell))
        {
            return true;
        }

        if (shell is { IsSheetShown: true, IsInstanceSheet: true, Instance.Expanded.IsConfirmingCancel: true })
        {
            // The frame's Cancel question: Esc answers Keep, as it does in the item sheet, and focus returns to the control that asked.
            shell.Instance.Expanded.Keep();
            _ = SheetPanel.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => string.Equals(b.Name, "CommandAsk", StringComparison.Ordinal) && b.IsEffectivelyVisible)?.Focus(Method());
            return true;
        }

        if (shell.Item.Actions.FirstOrDefault(a => a.IsConfirming) is { } asking)
        {
            asking.Keep();
            // L11: the question goes and focus returns to the control that asked it (the same path as a click on Keep).
            _ = this.GetVisualDescendants().OfType<Controls.CommandControl>().FirstOrDefault(c => ReferenceEquals(c.DataContext, asking))?.FocusRun();
            return true;
        }

        // §8: the filter is the last layer, after the sheet.
        if (shell.IsSheetShown || !shell.Queue.IsFiltering)
        {
            return false;
        }

        shell.Queue.ClearFilter();
        return true;
    }
}
