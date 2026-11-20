using Avalonia.Input;

namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>Names a key the way TerminalViewViewModel.Route reads it: "F6", "Shift+F6", "Ctrl+Shift+L", "Cmd+Shift+L", "Esc", "Tab", "Shift+Tab", "Enter", else the key's own name.</summary>
internal static class TerminalKeyNames
{
    /// <summary>True for a paste: Ctrl+Shift+V, Shift+Insert and Cmd+V. Plain Ctrl+V is not one: Claude Code pastes images on it and vim reads it as literal-next, so it goes to the agent as a key.</summary>
    /// <param name="name">A route name from <see cref="From"/>.</param>
    /// <returns>True when the key pastes.</returns>
    internal static bool IsPaste(string name) => name is "Ctrl+Shift+V" or "Cmd+V" or "Shift+Insert";

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
