using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>One cell of the pane's grid.</summary>
/// <param name="Text">The text drawn here: one character plus any combining marks, a space for a blank cell, and empty for the right half of a wide character.</param>
/// <param name="Fg">The foreground colour.</param>
/// <param name="Bg">The background colour.</param>
/// <param name="Style">The attributes.</param>
/// <param name="Columns">1 for a normal cell, 2 for the left half of a wide character, 0 for its right half.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TerminalCell(string Text, TerminalColor Fg, TerminalColor Bg, TerminalStyle Style, int Columns)
{
    /// <summary>A blank cell in the default colours.</summary>
    internal static TerminalCell Blank { get; } = new(" ", TerminalColor.Default, TerminalColor.Default, TerminalStyle.None, 1);

    /// <summary>True when the cell is bold.</summary>
    internal bool Bold => Style.HasFlag(TerminalStyle.Bold);

    /// <summary>True when the cell is italic.</summary>
    internal bool Italic => Style.HasFlag(TerminalStyle.Italic);

    /// <summary>True when the cell is underlined.</summary>
    internal bool Underline => Style.HasFlag(TerminalStyle.Underline);

    /// <summary>True when the cell is dim.</summary>
    internal bool Dim => Style.HasFlag(TerminalStyle.Dim);

    /// <summary>True when foreground and background swap.</summary>
    internal bool Inverse => Style.HasFlag(TerminalStyle.Inverse);
}
