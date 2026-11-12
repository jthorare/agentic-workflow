namespace Aw.Sindri.Core.Launch;

/// <summary>One whole-screen copy of a pane (agents spec §15.8, the 'frame' message).</summary>
/// <param name="Seq">The frame number, up from 1.</param>
/// <param name="Rows">The screen rows with their ANSI escapes, as 'capture-pane -e -p' returns them.</param>
/// <param name="CursorX">The cursor column.</param>
/// <param name="CursorY">The cursor row.</param>
/// <param name="Width">The pane width.</param>
/// <param name="Height">The pane height.</param>
/// <param name="Alt">True while the pane shows the alternate screen.</param>
/// <param name="InMode">True while the pane is in a tmux mode (copy mode).</param>
/// <param name="Dead">True when the pane's process has exited.</param>
public sealed class TerminalFrame(long Seq, IReadOnlyList<string> Rows, int CursorX, int CursorY, int Width, int Height, bool Alt, bool InMode, bool Dead)
{
    /// <summary>The frame number.</summary>
    public long Seq { get; } = Seq;

    /// <summary>The screen rows.</summary>
    public IReadOnlyList<string> Rows { get; } = Rows;

    /// <summary>The cursor column.</summary>
    public int CursorX { get; } = CursorX;

    /// <summary>The cursor row.</summary>
    public int CursorY { get; } = CursorY;

    /// <summary>The pane width.</summary>
    public int Width { get; } = Width;

    /// <summary>The pane height.</summary>
    public int Height { get; } = Height;

    /// <summary>Whether the alternate screen is up.</summary>
    public bool Alt { get; } = Alt;

    /// <summary>Whether the pane is in a tmux mode.</summary>
    public bool InMode { get; } = InMode;

    /// <summary>Whether the pane's process has exited.</summary>
    public bool Dead { get; } = Dead;
}
