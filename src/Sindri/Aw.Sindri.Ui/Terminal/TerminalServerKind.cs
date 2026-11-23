namespace Aw.Sindri.Ui.Terminal;

/// <summary>The server messages of the terminal WebSocket the window reads (docs/sindri/agents.md, the message table).</summary>
internal enum TerminalServerKind
{
    /// <summary>A message outside the table: dropped.</summary>
    Unknown,

    /// <summary>`ready`: the pane is attached.</summary>
    Ready,

    /// <summary>`frame`: the whole screen.</summary>
    Frame,

    /// <summary>`ping`: answer with a pong.</summary>
    Ping,
}
