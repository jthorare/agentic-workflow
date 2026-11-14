namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>What became of a key the person typed into the pane.</summary>
internal enum TerminalKeyFate
{
    /// <summary>The socket is ready: send it now.</summary>
    Sent,

    /// <summary>`ready` has not arrived: it waits up to 1 s.</summary>
    Queued,

    /// <summary>The socket is gone or the wait ran out: nothing is sent and the header word flashes.</summary>
    Dropped,

    /// <summary>A paste over the socket's size cap: nothing is sent; the page says so (#3729).</summary>
    TooBig,
}
