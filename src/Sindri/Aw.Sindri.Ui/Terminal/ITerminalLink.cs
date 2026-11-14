namespace Aw.Sindri.Ui.Terminal;

/// <summary>An open terminal socket: server messages in, client messages out. Closing it detaches and never stops the session.</summary>
internal interface ITerminalLink : IAsyncDisposable
{
    /// <summary>The WebSocket close code once the socket ended; null while it is open.</summary>
    public int? CloseCode { get; }

    /// <summary>Reads server messages until the socket ends.</summary>
    /// <param name="cancellationToken">Ends the read.</param>
    /// <returns>The messages in order.</returns>
    public IAsyncEnumerable<TerminalServerMessage> Read(CancellationToken cancellationToken);

    /// <summary>Sends one client message (JSON text).</summary>
    /// <param name="json">The message.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>True when it was sent.</returns>
    public Task<bool> Send(string json, CancellationToken cancellationToken);
}
