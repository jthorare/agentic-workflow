using Avalonia.Input;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>Names a key the way <c>TerminalViewViewModel.Route</c> reads it: "F6", "Shift+F6", "Ctrl+Shift+L", "Cmd+Shift+L", "Esc", "Tab", "Shift+Tab", "Enter", else the key's own name.</summary>
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
        return key switch
        {
            Key.Escape => prefix == string.Empty ? "Esc" : prefix + "Esc",
            Key.Return => prefix == string.Empty ? "Enter" : prefix + "Enter",
            _ => prefix + key,
        };
    }
}
