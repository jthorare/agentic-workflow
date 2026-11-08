using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the toast's keys (todo spec §3.3): Ctrl+Z (⌘Z) runs its Undo, Esc dismisses it while focus is on it.</summary>
internal sealed partial class ShellWindow
{
    private ToastHost ToastPanel => this.Get<ToastHost>("Toast");

    /// <summary>Whether a key is the Undo key: Z with Ctrl, or ⌘ on macOS.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">Its modifiers.</param>
    /// <param name="macOS">Whether the app runs on macOS.</param>
    /// <returns>True for Ctrl+Z (⌘Z).</returns>
    internal static bool IsToastUndoKey(Key key, KeyModifiers modifiers, bool macOS) =>
        key == Key.Z && modifiers == (macOS ? KeyModifiers.Meta : KeyModifiers.Control);

    // Ctrl+Z (⌘Z) outside a text field and over no layer; in a text field it is the field's own undo.
    private bool ToastUndoKey(KeyEventArgs e) =>
        Shell is { } shell && !OverlayOpen && IsToastUndoKey(e.Key, e.KeyModifiers, OperatingSystem.IsMacOS())
        && shell.Toast.PressUndoKey(inTextField: e.Source is TextBox);

    // The toast hid (it timed out, or Undo was clicked): when focus was on it and so is nowhere now, it goes to the page region like after Esc.
    internal void ToastHidden() =>
        Dispatcher.UIThread.Post(() =>
        {
            if (FocusManager?.GetFocusedElement() is null or Visual { IsEffectivelyVisible: false })
            {
                _ = PagePanel.Focus(NavigationMethod.Tab);
            }
        }, DispatcherPriority.Loaded);

    // Esc dismisses the toast only while focus is on it; focus then returns to the page region.
    private bool EscapeToast(ShellViewModel shell)
    {
        if (!shell.Toast.PressEscape(ToastPanel.IsKeyboardFocusWithin))
        {
            return false;
        }

        Dispatcher.UIThread.Post(() => _ = PagePanel.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        return true;
    }
}
