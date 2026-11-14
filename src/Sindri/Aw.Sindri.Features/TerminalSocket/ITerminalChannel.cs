namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>The WebSocket under <see cref="TerminalSocketSession"/>: text messages in and out, and a close (agents spec §15.8). The endpoint wraps the real socket; tests use a fake.</summary>
internal interface ITerminalChannel
{
    /// <summary>Waits for the next text message.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The message, or null when the peer closed.</returns>
    public ValueTask<string?> Receive(CancellationToken cancellationToken);

    /// <summary>Sends one text message.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that ends when the message is sent.</returns>
    public ValueTask Send(string json, CancellationToken cancellationToken);

    /// <summary>Closes the socket.</summary>
    /// <param name="code">The WebSocket close code (4015, 4016, 4001, 1001, 1008, 1009).</param>
    /// <param name="reason">The reason text ('SND-AGENT-015').</param>
    /// <param name="cancellationToken">Cancels the close.</param>
    /// <returns>A task that ends when the socket is closed.</returns>
    public ValueTask Close(int code, string reason, CancellationToken cancellationToken);
}
