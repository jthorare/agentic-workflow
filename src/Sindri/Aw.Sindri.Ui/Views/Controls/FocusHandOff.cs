using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>
/// The hand-off for a control that is about to hide while it holds keyboard focus (UX pass C-4, B-4): a hidden control drops focus to nothing, and with nothing focused no shortcut reaches the app.
/// Call it before the control goes; the control that takes over is the one the person would use next.
/// </summary>
internal static class FocusHandOff
{
    /// <summary>Moves focus to <paramref name="next"/> when <paramref name="leaving"/> holds it; otherwise changes nothing.</summary>
    /// <param name="leaving">The control that is about to hide, collapse or be removed.</param>
    /// <param name="next">The control that takes focus.</param>
    internal static void Before(Control leaving, Control next)
    {
        ArgumentNullException.ThrowIfNull(leaving);
        ArgumentNullException.ThrowIfNull(next);
        if (leaving.IsFocused || leaving.IsKeyboardFocusWithin)
        {
            _ = next.Focus();
        }
    }
}
