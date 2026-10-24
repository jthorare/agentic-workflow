namespace Aw.UiDriver.Core.Selectors;

#pragma warning disable MA0182 // used by the Avalonia adapter and the CLI, which the analyzer can't see from here

/// <summary>A key chord read from text: the key's name and the modifiers held with it.</summary>
/// <param name="Key">The key's name (an Avalonia Key member, as written).</param>
/// <param name="Control">Ctrl is held.</param>
/// <param name="Shift">Shift is held.</param>
/// <param name="Alt">Alt is held.</param>
/// <param name="Meta">The platform key (Cmd, Win, Super) is held.</param>
internal sealed record KeyChord(string Key, bool Control, bool Shift, bool Alt, bool Meta)
{
    /// <summary>Reads 'Ctrl+Shift+K'. Modifier names (Ctrl, Control, Shift, Alt, Meta, Cmd, Command, Win, Super) come before the key; the key is the last part.</summary>
    /// <param name="text">The chord.</param>
    /// <param name="chord">The chord, when the text is one.</param>
    /// <returns>True for a chord with a key and only known modifiers, none twice.</returns>
    internal static bool TryParse(string? text, out KeyChord chord)
    {
        chord = new KeyChord(string.Empty, Control: false, Shift: false, Alt: false, Meta: false);
        var parts = text?.Split('+', StringSplitOptions.TrimEntries);
        if (parts is not { Length: > 0 } || parts[^1].Length == 0)
        {
            return false;
        }

        (bool Control, bool Shift, bool Alt, bool Meta) held = default;
        foreach (var part in parts[..^1])
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL" or "CONTROL" when !held.Control:
                    held.Control = true;
                    break;
                case "SHIFT" when !held.Shift:
                    held.Shift = true;
                    break;
                case "ALT" when !held.Alt:
                    held.Alt = true;
                    break;
                case "META" or "CMD" or "COMMAND" or "WIN" or "SUPER" when !held.Meta:
                    held.Meta = true;
                    break;
                default:
                    return false;
            }
        }

        chord = new KeyChord(parts[^1], held.Control, held.Shift, held.Alt, held.Meta);
        return true;
    }
}
