namespace Aw.Sindri.Core.Launch;

/// <summary>Where a terminal stream's pane lives (agents spec §15.8, the 'ready' message).</summary>
/// <param name="Socket">The tmux socket name ('sindri-launch' or a foreign one).</param>
/// <param name="Session">The tmux session name.</param>
/// <param name="Pane">The pane id ('%3').</param>
public sealed class TerminalHost(string Socket, string Session, string Pane)
{
    /// <summary>The tmux socket name.</summary>
    public string Socket { get; } = Socket;

    /// <summary>The tmux session name.</summary>
    public string Session { get; } = Session;

    /// <summary>The pane id.</summary>
    public string Pane { get; } = Pane;
}
