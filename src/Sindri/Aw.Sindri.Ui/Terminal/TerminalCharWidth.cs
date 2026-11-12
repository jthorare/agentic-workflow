using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>How many columns a character takes in a terminal cell grid.</summary>
internal static class TerminalCharWidth
{
    // Inclusive pairs of East Asian wide and fullwidth ranges, and emoji blocks.
    private static readonly int[] _wideRanges =
    [
        0x1100, 0x115F, 0x2329, 0x232A, 0x2E80, 0x303E, 0x3040, 0xA4CF, 0xAC00, 0xD7A3, 0xF900, 0xFAFF,
        0xFE30, 0xFE6F, 0xFF00, 0xFF60, 0xFFE0, 0xFFE6, 0x1F300, 0x1F64F, 0x1F900, 0x1F9FF, 0x20000, 0x3FFFD,
    ];

    /// <summary>The columns of a character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>2 for wide, 1 for normal, 0 for a combining mark (it joins the cell before it), -1 for a control character (drawn as nothing).</returns>
    internal static int Of(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        if (rune.Value is < 0x20 or (>= 0x7F and < 0xA0))
        {
            return rune.Value == '\t' ? 1 : -1;
        }

        return IsWide(rune.Value) ? 2 : 1;
    }

    private static bool IsWide(int value)
    {
        for (var i = 0; i < _wideRanges.Length; i += 2)
        {
            if (value >= _wideRanges[i] && value <= _wideRanges[i + 1])
            {
                return true;
            }
        }

        return false;
    }
}
