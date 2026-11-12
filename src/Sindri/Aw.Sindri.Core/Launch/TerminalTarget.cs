namespace Aw.Sindri.Core.Launch;

/// <summary>Which tmux pane a terminal stream targets (agents spec §15.2: every call targets the pane id).</summary>
/// <param name="Socket">The tmux socket: a name for the launch server, a path for a foreign one.</param>
/// <param name="SocketIsPath">True when <paramref name="Socket"/> is a path ('tmux -S'), false for a name ('tmux -L').</param>
/// <param name="Session">The tmux session name.</param>
/// <param name="Pane">The pane id ('%3').</param>
/// <param name="Foreign">True for a server Sindri did not create: the foreign allowlist applies and the window is never resized.</param>
public sealed class TerminalTarget(string Socket, bool SocketIsPath, string Session, string Pane, bool Foreign)
{
    /// <summary>The tmux socket.</summary>
    public string Socket { get; } = Socket;

    /// <summary>Whether the socket is a path.</summary>
    public bool SocketIsPath { get; } = SocketIsPath;

    /// <summary>The tmux session name.</summary>
    public string Session { get; } = Session;

    /// <summary>The pane id.</summary>
    public string Pane { get; } = Pane;

    /// <summary>Whether the server is foreign.</summary>
    public bool Foreign { get; } = Foreign;
}
