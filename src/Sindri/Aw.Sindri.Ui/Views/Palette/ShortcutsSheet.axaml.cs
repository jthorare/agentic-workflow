using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Palette;

namespace Aw.Sindri.Ui.Views.Palette;

/// <summary>The `?` sheet's view (UI spec §8): a click on the scrim closes it; the window handles Esc and focus.</summary>
internal sealed partial class ShortcutsSheet : UserControl
{
    private static readonly Dictionary<Key, Action<ScrollViewer>> _scrolls = new()
    {
        [Key.Down] = v => v.LineDown(),
        [Key.Up] = v => v.LineUp(),
        [Key.PageDown] = v => v.PageDown(),
        [Key.PageUp] = v => v.PageUp(),
        [Key.Home] = v => v.ScrollToHome(),
        [Key.End] = v => v.ScrollToEnd(),
    };

    public ShortcutsSheet()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Bubble);
    }

    // The card takes focus, not its list, so the keys that scroll a list scroll it from here: the last row is reachable
    // by keyboard (U1 run 1, a11y-7).
    private void OnKey(object? sender, KeyEventArgs e)
    {
        // The card is the sheet's only tab stop (the rows are text), so Cycle has nothing to wrap to and Tab would walk on to the
        // page behind the scrim: Tab and Shift+Tab stay on the card (#1081, C-15).
        if (e.Key == Key.Tab && (e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.None)
        {
            e.Handled = true;
            return;
        }

        if (!_scrolls.TryGetValue(e.Key, out var scroll))
        {
            return;
        }

        scroll(ShortcutsScroll);
        e.Handled = true;
    }

    /// <summary>Moves focus onto the sheet (the window calls it when the sheet opens).</summary>
    internal void FocusSheet() => ShortcutsCard.Focus(NavigationMethod.Tab);

    private void OnScrim(object? sender, PointerPressedEventArgs e) => (DataContext as ShortcutsViewModel)?.Close();
}
