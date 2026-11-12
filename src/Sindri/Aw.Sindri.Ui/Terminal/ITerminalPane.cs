namespace Aw.Sindri.Ui.Terminal;

/// <summary>The drawing side of the pane seam (agents page spec §15.7): draws the frames it is given and reports the size it can show. It never starts a process, so Serve's token check and allowlist cannot be bypassed. U1 (#3153) may define the same seam; whichever lands second reconciles.</summary>
#pragma warning disable MA0182 // Used by the drawing control in the next commit of #3383.
internal interface ITerminalPane
{
    /// <summary>Raised when the drawing area changes the number of columns or rows it fits.</summary>
    public event EventHandler<TerminalSizeEventArgs>? FitChanged;

    /// <summary>Draws a frame.</summary>
    /// <param name="grid">The frame.</param>
    public void Draw(TerminalGrid grid);
}
