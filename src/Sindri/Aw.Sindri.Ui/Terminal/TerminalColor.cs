using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>A colour of one cell, as the pane's capture-pane -e text named it.</summary>
/// <param name="Kind">How the colour is named.</param>
/// <param name="Value">The palette slot for <see cref="TerminalColorKind.Indexed"/>, 0xRRGGBB for <see cref="TerminalColorKind.Rgb"/>, 0 for the default.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TerminalColor(TerminalColorKind Kind, int Value)
{
    /// <summary>The terminal's default colour.</summary>
    internal static TerminalColor Default => new(TerminalColorKind.Default, 0);

    /// <summary>A palette slot.</summary>
    /// <param name="slot">0-255.</param>
    /// <returns>The colour.</returns>
    internal static TerminalColor Indexed(int slot) => new(TerminalColorKind.Indexed, slot);

    /// <summary>A 24-bit colour.</summary>
    /// <param name="r">Red.</param>
    /// <param name="g">Green.</param>
    /// <param name="b">Blue.</param>
    /// <returns>The colour.</returns>
    internal static TerminalColor FromRgb(int r, int g, int b) => new(TerminalColorKind.Rgb, (r << 16) | (g << 8) | b);
}
