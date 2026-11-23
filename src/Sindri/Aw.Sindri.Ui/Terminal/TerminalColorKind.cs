namespace Aw.Sindri.Ui.Terminal;

/// <summary>How a <see cref="TerminalColor"/> names its colour.</summary>
internal enum TerminalColorKind
{
    /// <summary>The terminal's own default (SGR 39 or 49): the view picks it from the theme.</summary>
    Default,

    /// <summary>A palette slot: 0-7 normal, 8-15 bright, 16-255 the 256-colour cube and grey ramp.</summary>
    Indexed,

    /// <summary>A 24-bit colour packed as 0xRRGGBB.</summary>
    Rgb,
}
