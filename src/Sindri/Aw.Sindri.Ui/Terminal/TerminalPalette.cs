using Avalonia.Media;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>Maps a <see cref="TerminalColor"/> to an RGB colour: the 16 ANSI slots, the 256-colour cube and grey ramp, and truecolour as given.</summary>
internal static class TerminalPalette
{
    private static readonly uint[] _ansi =
    [
        0x000000, 0xCD3131, 0x0DBC79, 0xE5E510, 0x2472C8, 0xBC3FBC, 0x11A8CD, 0xE5E5E5,
        0x666666, 0xF14C4C, 0x23D18B, 0xF5F543, 0x3B8EEA, 0xD670D6, 0x29B8DB, 0xFFFFFF,
    ];

    private static readonly byte[] _cubeLevels = [0, 95, 135, 175, 215, 255];

    /// <summary>Resolves a colour.</summary>
    /// <param name="color">The colour from a cell.</param>
    /// <returns>The RGB colour, or null for the terminal default (the view takes it from the theme).</returns>
    internal static Color? Resolve(TerminalColor color) => color.Kind switch
    {
        TerminalColorKind.Indexed => Indexed(color.Value),
        TerminalColorKind.Rgb => Color.FromRgb((byte)(color.Value >> 16), (byte)(color.Value >> 8), (byte)color.Value),
        TerminalColorKind.Default => null,
        _ => null,
    };

    private static Color Indexed(int slot)
    {
        if (slot < _ansi.Length)
        {
            var packed = _ansi[slot];
            return Color.FromRgb((byte)(packed >> 16), (byte)(packed >> 8), (byte)packed);
        }

        if (slot < 232)
        {
            var n = slot - 16;
            return Color.FromRgb(_cubeLevels[n / 36], _cubeLevels[n / 6 % 6], _cubeLevels[n % 6]);
        }

        var grey = (byte)(8 + (10 * (Math.Min(slot, 255) - 232)));
        return Color.FromRgb(grey, grey, grey);
    }
}
