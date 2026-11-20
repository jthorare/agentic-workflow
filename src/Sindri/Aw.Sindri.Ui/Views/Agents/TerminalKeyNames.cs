using Avalonia.Input;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>Names a key the way TerminalViewViewModel.Route reads it: "F6", "Shift+F6", "Ctrl+Shift+L", "Cmd+Shift+L", "Esc", "Tab", "Shift+Tab", "Enter", else the key's own name.</summary>
internal static class TerminalKeyNames
{
    /// <summary>Names one key press.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    /// <returns>The route name.</returns>
    internal static string From(Key key, KeyModifiers modifiers)
    {
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var prefix = (modifiers.HasFlag(KeyModifiers.Control) ? "Ctrl+" : string.Empty)
            + (modifiers.HasFlag(KeyModifiers.Meta) ? "Cmd+" : string.Empty)
            + (modifiers.HasFlag(KeyModifiers.Alt) ? "Alt+" : string.Empty)
            + (shift ? "Shift+" : string.Empty);
        if (key == Key.Escape)
        {
            return prefix.Length == 0 ? "Esc" : prefix + "Esc";
        }

        return key == Key.Return ? (prefix.Length == 0 ? "Enter" : prefix + "Enter") : prefix + key;
    }
}
