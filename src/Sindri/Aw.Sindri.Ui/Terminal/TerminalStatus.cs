using System.Globalization;
using System.Runtime.InteropServices;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>The display-message line of a frame: cursor, size and pane state.</summary>
/// <param name="Width">Columns, null when the line didn't parse.</param>
/// <param name="Height">Rows, null when the line didn't parse.</param>
/// <param name="CursorX">Cursor column.</param>
/// <param name="CursorY">Cursor row.</param>
/// <param name="InMode">True in a tmux mode.</param>
/// <param name="Alternate">True on the alternate screen.</param>
/// <param name="Dead">True when the pane's process has exited.</param>
/// <param name="DeadStatus">The exit status when tmux gave one.</param>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct TerminalStatus(int? Width, int? Height, int CursorX, int CursorY, bool InMode, bool Alternate, bool Dead, int? DeadStatus)
{
    /// <summary>The widest pane the grid will allocate (the terminal WebSocket's size limit): a corrupt width of 2 billion must not allocate 2 billion cells.</summary>
    internal const int MaxColumns = 400;

    /// <summary>The tallest pane the grid will allocate (the terminal WebSocket's size limit).</summary>
    internal const int MaxRows = 200;

    /// <summary>What a line that doesn't parse reads as: no size, the cursor at 0,0, nothing set.</summary>
    internal static TerminalStatus Unknown => new(Width: null, Height: null, CursorX: 0, CursorY: 0, InMode: false, Alternate: false, Dead: false, DeadStatus: null);

    /// <summary>Reads the line cursor_x cursor_y width height in_mode alternate_on dead dead_status.</summary>
    /// <param name="line">The display-message output.</param>
    /// <returns>The status, or <see cref="Unknown"/>.</returns>
    internal static TerminalStatus Parse(string line)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 7)
        {
            return Unknown;
        }

        var n = new int[7];
        for (var i = 0; i < n.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out n[i]))
            {
                return Unknown;
            }
        }

        int? deadStatus = parts.Length > 7 && int.TryParse(parts[7], NumberStyles.None, CultureInfo.InvariantCulture, out var s) ? s : null;
        return new TerminalStatus(Math.Clamp(n[2], 1, MaxColumns), Math.Clamp(n[3], 1, MaxRows), n[0], n[1], n[4] != 0, n[5] != 0, n[6] != 0, deadStatus);
    }
}
