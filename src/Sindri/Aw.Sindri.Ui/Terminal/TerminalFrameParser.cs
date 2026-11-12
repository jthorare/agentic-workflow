namespace Aw.Sindri.Ui.Terminal;

/// <summary>Parses the text of one pane frame into a <see cref="TerminalGrid"/> (way C, agents page spec §15.7). A pure function: no tmux call, no clock.</summary>
#pragma warning disable MA0182 // Used by the drawing control in the next commit of #3383.
internal static class TerminalFrameParser
{
    /// <summary>Parses a frame.</summary>
    /// <param name="capture">The output of tmux capture-pane -e -p: one line per row, SGR codes inline. More lines than the pane is high means the capture reached into scrollback (-S): the extra leading lines are kept as history.</param>
    /// <param name="status">The output of display-message -p '#{cursor_x} #{cursor_y} #{pane_width} #{pane_height} #{pane_in_mode} #{alternate_on} #{pane_dead} #{pane_dead_status}'. A line that doesn't parse leaves the cursor at 0,0 and takes the size from the capture, so a missed call still draws.</param>
    /// <returns>The grid.</returns>
    internal static TerminalGrid Parse(string capture, string status) => throw new NotSupportedException("U7b: not written yet.");
}
