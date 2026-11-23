namespace Aw.Sindri.Ui.Views.Agents;

/// <summary>Shift+PageUp or Shift+PageDown in the pane (#3730): screens of tmux history to move.</summary>
/// <param name="pages">1 is one screen further back, -1 one screen towards live.</param>
internal sealed class TerminalScrollEventArgs(int pages) : EventArgs
{
    /// <summary>Screens to move: positive back, negative towards live.</summary>
    public int Pages { get; } = pages;
}
