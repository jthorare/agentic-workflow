namespace Aw.Sindri.Ui.Terminal;

/// <summary>One frame of the pane: the cells, the cursor and the pane's state, parsed from a capture-pane and a display-message (agents page spec §15.7).</summary>
internal sealed class TerminalGrid
{
    private readonly TerminalCell[] _cells;

    /// <summary>Builds a grid.</summary>
    /// <param name="width">Columns.</param>
    /// <param name="height">Rows of the live screen.</param>
    /// <param name="history">Rows of scrollback above the live screen.</param>
    /// <param name="cells">Row-major cells, (history + height) * width of them.</param>
    internal TerminalGrid(int width, int height, int history, TerminalCell[] cells)
    {
        Width = width;
        Height = height;
        History = history;
        _cells = cells;
    }

    /// <summary>Columns.</summary>
    internal int Width { get; }

    /// <summary>Rows of the live screen.</summary>
    internal int Height { get; }

    /// <summary>Rows of scrollback above the live screen (0 when the capture held only the screen).</summary>
    internal int History { get; }

    /// <summary>All rows: <see cref="History"/> then the live screen.</summary>
    internal int Rows => History + Height;

    /// <summary>The cursor column, 0-based.</summary>
    internal int CursorX { get; init; }

    /// <summary>The cursor row within the live screen, 0-based.</summary>
    internal int CursorY { get; init; }

    /// <summary>True when the pane is on the alternate screen (Claude Code's full-screen UI).</summary>
    internal bool AlternateScreen { get; init; }

    /// <summary>True when the pane is in a tmux mode (copy mode and so on).</summary>
    internal bool InMode { get; init; }

    /// <summary>True when the pane's process has exited.</summary>
    internal bool Dead { get; init; }

    /// <summary>The exit status of a dead pane, null while alive or when tmux gave none.</summary>
    internal int? DeadStatus { get; init; }

    /// <summary>One cell.</summary>
    /// <param name="row">0 to <see cref="Rows"/> - 1; the first <see cref="History"/> rows are scrollback.</param>
    /// <param name="column">0 to <see cref="Width"/> - 1.</param>
    /// <returns>The cell.</returns>
    internal TerminalCell Cell(int row, int column) => _cells[(row * Width) + column];

    /// <summary>The text of one row, trailing blanks trimmed, the right half of a wide character left out.</summary>
    /// <param name="row">0 to <see cref="Rows"/> - 1.</param>
    /// <returns>The text.</returns>
    internal string RowText(int row)
    {
        var text = new System.Text.StringBuilder(Width);
        for (var c = 0; c < Width; c++)
        {
            text.Append(Cell(row, c).Text);
        }

        return text.ToString().TrimEnd(' ');
    }

    /// <summary>The same frame cut or padded to a new size: the view calls it at once on a window resize, before the next frame from tmux arrives. Rows keep their bottom edge, so the live screen stays at the bottom.</summary>
    /// <param name="width">New columns.</param>
    /// <param name="height">New live rows.</param>
    /// <returns>The resized grid.</returns>
    internal TerminalGrid Resize(int width, int height) => throw new NotSupportedException("U7b: not written yet.");
}
