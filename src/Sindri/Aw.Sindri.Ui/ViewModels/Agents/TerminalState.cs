namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>The header's one state word (agents spec §15.3 "Header"): it describes the socket and the session, never focus.</summary>
internal enum TerminalState
{
    /// <summary>The socket is opening.</summary>
    Connecting,

    /// <summary>The socket dropped and tmux is fine.</summary>
    Reconnecting,

    /// <summary>No socket for 30 s.</summary>
    CantReachSindri,

    /// <summary>Close code 4001: a second view opened the same pane.</summary>
    TakenOver,

    /// <summary>A one-off session's agent is coming up.</summary>
    Starting,

    /// <summary>The pane is live.</summary>
    Attached,

    /// <summary>The session has no tmux pane (§15.5).</summary>
    CantAttach,

    /// <summary>The tmux server or session is gone (SND-AGENT-016).</summary>
    Gone,

    /// <summary>The agent exited; tmux is still there.</summary>
    Ended,
}
