namespace Aw.Sindri.Core.Launch;

/// <summary>One whole-screen copy of a pane (agents spec §15.8, the 'frame' message). A record: it is data, and its nine positional fields and the two history fields are the wire shape.</summary>
/// <param name="Seq">The frame number, up from 1.</param>
/// <param name="Rows">The screen rows with their ANSI escapes, as 'capture-pane -e -p' returns them.</param>
/// <param name="CursorX">The cursor column.</param>
/// <param name="CursorY">The cursor row.</param>
/// <param name="Width">The pane width.</param>
/// <param name="Height">The pane height.</param>
/// <param name="Alt">True while the pane shows the alternate screen.</param>
/// <param name="InMode">True while the pane is in a tmux mode (copy mode).</param>
/// <param name="Dead">True when the pane's process has exited.</param>
public sealed record TerminalFrame(long Seq, IReadOnlyList<string> Rows, int CursorX, int CursorY, int Width, int Height, bool Alt, bool InMode, bool Dead)
{
    /// <summary>The rows above the live screen's top that this frame shows (the 'scroll' message's offset, clamped); 0 is the live screen. 0 on the alternate screen.</summary>
    public int Back { get; init; }

    /// <summary>The rows of tmux history the pane holds ('#{history_size}'), or 0 on the alternate screen, which has no scrollback.</summary>
    public int Hist { get; init; }
}
