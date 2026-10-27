using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Palette;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Approvals;
using Aw.Sindri.Ui.Views.Item;
using Aw.Sindri.Ui.Views.Layout;
using Aw.Sindri.Ui.Views.Palette;
using Aw.Sindri.Ui.Views.Start;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>The shell window (continued): the palette and the `?` sheet's keys and focus (UI spec §8, §11.2).</summary>
internal sealed partial class ShellWindow
{
    // ⌘K opens the palette, `?` the shortcuts sheet (never while typing in a field, nor over the palette), ⌘, Settings.
    private bool OpenByKey(KeyEventArgs e)
    {
        if (Shell is not { IsChromeVisible: true } shell)
        {
            return false;
        }

        if (FormOpen(shell))
        {
            return SwallowedByForm(e);
        }

        if (OpenLayerByChord(e, shell))
        {
            return true;
        }

        if (IsQuestionMark(e) && e.Source is not TextBox && !shell.Palette.IsOpen)
        {
            shell.Shortcuts.Open();
            return true;
        }

        return OpenSettingsByKey(e, shell);
    }

    private static bool FormOpen(ShellViewModel shell) => shell.Create.IsOpen || shell.Start.IsOpen || shell.ApproveAll.IsOpen;

    // ⌘K opens the palette, ⌘N the Start form: one layer at a time, so the one open goes first (focus goes back to the original opener first, OnOverlayChanged).
    private static bool OpenLayerByChord(KeyEventArgs e, ShellViewModel shell)
    {
        if (e.KeyModifiers != _command || e.Key is not (Key.K or Key.N))
        {
            return false;
        }

        shell.Shortcuts.Close();
        if (e.Key == Key.N)
        {
            shell.Palette.Close();
            shell.OpenStart();
        }
        else
        {
            shell.Palette.Open();
        }

        return true;
    }

    // The Create and Start forms are modal (L10, §11.2; U5'): ⌘K, ⌘, and ? are swallowed. ? is a character in a field, so it types there.
    private static bool SwallowedByForm(KeyEventArgs e) =>
        (e.Key is Key.K or Key.OemComma && e.KeyModifiers == _command) || (IsQuestionMark(e) && e.Source is not TextBox);
    private bool _openingSettings;

    private bool OpenSettingsByKey(KeyEventArgs e, ShellViewModel shell)
    {
        if (e.Key != Key.OemComma || e.KeyModifiers != _command)
        {
            return false;
        }

        if (OverlayOpen)
        {
            return true;   // the palette and the ? sheet are modal (§11.2): the chord is swallowed, focus stays in the card
        }

        _openingSettings = true;   // the page change would post KeepFocus too; ⌘, moves focus once, into Settings
        try
        {
            shell.GoTo(PageId.Settings);
        }
        finally
        {
            _openingSettings = false;
        }

        // Like the sheet's heading (§11.2), opening Settings moves focus into it: its first control.
        Dispatcher.UIThread.Post(() => FirstFocusable(this.Get<Control>("SettingsSlot"))?.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
        return true;
    }

    // A page's button is about to open an overlay: closing it returns focus there even when a click left focus on the page region.
    private void NoteInvoker(object? sender, OpenerNoteEventArgs e) => _invoker = e.Invoker;

    private bool OverlayOpen => Shell is { } shell && (shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen || shell.Start.IsOpen || shell.ApproveAll.IsOpen);

    // `?` is Shift+/ on most layouts; the key symbol says so on the rest.
    private static bool IsQuestionMark(KeyEventArgs e) =>
        e.KeySymbol == "?" || (e.Key == Key.OemQuestion && e.KeyModifiers == KeyModifiers.Shift);

    // The palette, the ? sheet, the Create form or the Start form opened: remember what had focus and move focus into it. Closed: give focus back to what
    // had it (the ⌘K field, a page), or the ⌘K field when that is gone.
    private void OnOverlayChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandPaletteViewModel.IsOpen) || Shell is not { } shell)
        {
            return;
        }

        if (IsOpen(sender, shell))
        {
            _overlayOpener ??= OverlayOpener.Choose(FocusManager?.GetFocusedElement() as Control, this.Get<Control>("Page"), _invoker);
            _invoker = null;
            Dispatcher.UIThread.Post(() => FocusOverlay(sender, shell), DispatcherPriority.Loaded);
            return;
        }

        if (OverlayOpen)
        {
            return;   // closing because another overlay opened: its opener goes on to that one, with no focus move in between
        }

        if (sender == shell.Start && shell.Start.OpenedFrame)
        {
            // An accepted start opened the new instance's frame: focus goes there, not back to what opened the form.
            _overlayOpener = null;
            Dispatcher.UIThread.Post(FocusOpenedSheet, DispatcherPriority.Loaded);
            return;
        }

        var back = Usable(_overlayOpener) ?? Usable(FallbackAfter(sender, shell));
        _overlayOpener = null;
        _ = back?.Focus(Method());   // a pointer flow draws no ring; a keyboard one does
    }

    private static bool IsOpen(object? sender, ShellViewModel shell) =>
        sender == shell.Palette ? shell.Palette.IsOpen : sender == shell.Create ? shell.Create.IsOpen : sender == shell.Start ? shell.Start.IsOpen : sender == shell.ApproveAll ? shell.ApproveAll.IsOpen : shell.Shortcuts.IsOpen;

    // Where focus goes when what opened the overlay is gone: the Create form falls back to the Queue page's "Create item"
    // button, the others to the ⌘K field.
    private Control? FallbackAfter(object? sender, ShellViewModel shell) =>
        Usable(sender == shell.Create ? this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "CreateButton") : null)
            ?? Usable(TopBar.Get<Control>("SearchField"));

    private void FocusOverlay(object? sender, ShellViewModel shell)
    {
        if (sender == shell.Palette)
        {
            this.Get<CommandPalette>("CommandPalette").FocusQuery();
        }
        else if (sender == shell.Create)
        {
            this.Get<CreateItemDialog>("CreateItemDialog").FocusTitle();
        }
        else if (sender == shell.Start)
        {
            this.Get<StartDialog>("StartDialog").FocusFirst();
        }
        else if (sender == shell.ApproveAll)
        {
            this.Get<ApproveAllDialog>("ApproveAllDialog").FocusFirst();
        }
        else
        {
            this.Get<ShortcutsSheet>("ShortcutsSheet").FocusSheet();
        }
    }
}
