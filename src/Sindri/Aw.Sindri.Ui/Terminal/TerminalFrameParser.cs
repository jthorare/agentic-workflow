namespace Aw.Sindri.Ui.Terminal;

/// <summary>Parses the text of one pane frame into a <see cref="TerminalGrid"/> (way C, agents page spec §15.7). A pure function: no tmux call, no clock.</summary>
#pragma warning disable MA0182 // Used by the drawing control in the next commit of #3383.
internal static class TerminalFrameParser
{
    /// <summary>Parses a frame.</summary>
    /// <param name="capture">The output of capture-pane -e -p: one line per row, SGR codes inline. More lines than the pane is high means the capture reached into scrollback (-S): the extra leading lines are kept as history.</param>
    /// <param name="status">The output of display-message -p '#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead} #{pane_dead_status}'. A line that doesn't parse leaves the cursor at 0,0 and takes the size from the capture, so a missed call still draws.</param>
    /// <returns>The grid.</returns>
    internal static TerminalGrid Parse(string capture, string status) => Parse(SplitLines(capture), TerminalStatus.Parse(status));

    /// <summary>Parses a frame that arrived as lines plus its status, as the terminal WebSocket's frame message carries it.</summary>
    /// <param name="lines">One ANSI line per row, no line endings.</param>
    /// <param name="info">The cursor, size and pane state.</param>
    /// <returns>The grid.</returns>
    internal static TerminalGrid Parse(IReadOnlyList<string> lines, TerminalStatus info)
    {
        var pen = new TerminalPen();
        var rows = lines.Select(line => TerminalRowParser.Parse(line, pen)).ToList();
        var width = info.Width ?? Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Count));
        var height = info.Height ?? Math.Max(1, rows.Count);
        while (rows.Count < height)
        {
            rows.Add([]);
        }

        return new TerminalGrid(width, height, rows.Count - height, Cells(rows, width))
        {
            CursorX = info.CursorX,
            CursorY = info.CursorY,
            InMode = info.InMode,
            AlternateScreen = info.Alternate,
            Dead = info.Dead,
            DeadStatus = info.Dead ? info.DeadStatus : null,
        };
    }

    private static TerminalCell[] Cells(List<List<TerminalCell>> rows, int width)
    {
        var cells = new TerminalCell[rows.Count * width];
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < width; c++)
            {
                cells[(r * width) + c] = c < row.Count ? row[c] : TerminalCell.Blank;
            }

            // A wide character cut in half by the right edge is dropped.
            if (row.Count > width && cells[(r * width) + width - 1].Columns == 2)
            {
                cells[(r * width) + width - 1] = TerminalCell.Blank;
            }
        }

        return cells;
    }

    private static List<string> SplitLines(string capture)
    {
        var text = capture.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.EndsWith('\n'))
        {
            text = text[..^1];
        }

        return text.Length == 0 ? [] : [.. text.Split('\n')];
    }
}
