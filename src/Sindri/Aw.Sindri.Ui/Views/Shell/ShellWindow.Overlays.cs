using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Palette;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Item;
using Aw.Sindri.Ui.Views.Layout;
using Aw.Sindri.Ui.Views.Linear;
using Aw.Sindri.Ui.Views.Palette;
using Aw.Sindri.Ui.Views.Todo;

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

        if (QuickAddByKey(e))
        {
            return true;
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

    // N opens the quick-add box (never in a text field or over a layer), and so does Ctrl+Shift+N (⌘⇧N) over a page or the palette (todo spec §3.3).
    // Both reach the view model even while Sindri isn't running, which says so instead of opening the box.
    // This runs before every other key handler, so a page that takes N for itself must be guarded here (none does yet: the read-only Workflows page has no Start).
    private bool QuickAddByKey(KeyEventArgs e)
    {
        if (e.Key != Key.N || Shell is not { IsChromeVisible: true } shell)
        {
            return false;
        }

        if (e.KeyModifiers == (_command | KeyModifiers.Shift))
        {
            return shell.PressQuickAdd();
        }

        return e.KeyModifiers == KeyModifiers.None && e.Source is not TextBox && !AnyLayerOpen(shell) && shell.PressQuickAdd();
    }

    private static bool FormOpen(ShellViewModel shell) => shell.Create.IsOpen || shell.NewIssue.IsOpen;

    // ⌘K opens the palette, ⌘N the Create form (like C): one layer at a time, so the one open goes first (focus goes back to the original opener first, OnOverlayChanged).
    private static bool OpenLayerByChord(KeyEventArgs e, ShellViewModel shell)
    {
        if (e.KeyModifiers != _command || e.Key is not (Key.K or Key.N))
        {
            return false;
        }

        shell.Shortcuts.Close();
        shell.QuickAdd.Close();
        if (e.Key == Key.K)
        {
            shell.Palette.Open();
        }
        else
        {
            shell.Palette.Close();
            shell.Create.Open();
        }

        return true;
    }

    // The Create and New issue forms are modal (L10, §11.2; U5'): ⌘K, ⌘, and ? are swallowed. ? is a character in a field, so it types there.
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

        // Like the sheet's heading (§11.2), opening Settings moves focus into it: the filter box (#3362), else its first control (the title is focusable and comes first).
        Dispatcher.UIThread.Post(() =>
        {
            var slot = this.Get<Control>("SettingsSlot");
            var target = slot.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(static t => string.Equals(t.Name, "SettingsFilterBox", StringComparison.Ordinal) && t.IsEffectivelyVisible) ?? FirstFocusable(slot);
            _ = target?.Focus(NavigationMethod.Tab);
            slot.FindAncestorOfType<ScrollViewer>()?.ScrollToHome();   // the box brings itself into view and over-scrolls: the title stays under the top bar otherwise (#3453)
        }, DispatcherPriority.Loaded);
        return true;
    }

    // A page's button is about to open an overlay: closing it returns focus there even when a click left focus on the page region.
    private void NoteInvoker(object? sender, OpenerNoteEventArgs e) => _invoker = e.Invoker;

    private bool OverlayOpen => Shell is { } shell && (shell.Palette.IsOpen || shell.Shortcuts.IsOpen || shell.Create.IsOpen || shell.NewIssue.IsOpen || shell.QuickAdd.IsOpen);

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

        if (ClosedForSomethingElse(sender, shell))
        {
            return;
        }

        var back = Usable(_overlayOpener) ?? Usable(FallbackAfter(sender, shell));
        _overlayOpener = null;
        _ = back?.Focus(Method());   // a pointer flow draws no ring; a keyboard one does
    }

    // A create closed the dialog and asked the list to focus the new row: focus goes there,
    // not back to what opened the form.
    private bool ClosedForSomethingElse(object? sender, ShellViewModel shell)
    {
        if (sender == shell.NewIssue && shell.NewIssue.JustCreated)
        {
            _overlayOpener = null;
            return true;
        }

        return false;
    }

    private void ListenLinear(ShellViewModel shell, bool on)
    {
        if (on)
        {
            shell.Linear.SelectMissed += OnSelectMissed;
        }
        else
        {
            shell.Linear.SelectMissed -= OnSelectMissed;
        }
    }

    // A created issue the list's filters hide has no row to take focus: the page's Create button does, as it would after the dialog closed without a create.
    private void OnSelectMissed(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(
            () =>
            {
                if (Shell is { } shell)
                {
                    _ = Usable(FallbackAfter(shell.NewIssue, shell))?.Focus(Method());
                }
            },
            DispatcherPriority.Loaded);

    private static bool IsOpen(object? sender, ShellViewModel shell) =>
        sender == shell.Palette ? shell.Palette.IsOpen : sender == shell.Create ? shell.Create.IsOpen : sender == shell.NewIssue ? shell.NewIssue.IsOpen : sender == shell.QuickAdd ? shell.QuickAdd.IsOpen : shell.Shortcuts.IsOpen;

    // Where focus goes when what opened the overlay is gone: the Create form falls back to the Queue page's "Create item"
    // button, the others to the ⌘K field.
    private Control? FallbackAfter(object? sender, ShellViewModel shell) =>
        Usable(sender == shell.Create || sender == shell.NewIssue ? this.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "CreateButton") : null)
            ?? Usable(TopBar.Get<Control>("SearchField"));

    // The Create item form opens on its title, the Linear New issue form on its team.
    private void FocusForm(object? sender, ShellViewModel shell)
    {
        if (sender == shell.Create)
        {
            Built<CreateItemDialog>("CreateItemDialog")?.FocusTitle();
        }
        else
        {
            Built<NewLinearIssueDialog>("NewLinearIssueDialog")?.FocusTeam();
        }
    }

    private void FocusOverlay(object? sender, ShellViewModel shell)
    {
        if (sender == shell.Palette)
        {
            Built<CommandPalette>("CommandPalette")?.FocusQuery();
        }
        else if (sender == shell.Create || sender == shell.NewIssue)
        {
            FocusForm(sender, shell);
        }
        else if (sender == shell.QuickAdd)
        {
            Built<QuickAddBox>("QuickAddBox")?.FocusField();
        }
        else
        {
            Built<ShortcutsSheet>("ShortcutsSheet")?.FocusSheet();
        }
    }
}
