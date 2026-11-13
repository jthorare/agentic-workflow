using System.Net.WebSockets;
using System.Text;
using Aw.Sindri.Features.TerminalSocket;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The real WebSocket under a terminal session: whole text messages in (at most 512 KiB, a paste is 256 KiB), one send at a time out.</summary>
internal sealed class WebSocketTerminalChannel(WebSocket socket) : ITerminalChannel, IDisposable
{
    private const int MaxMessageBytes = 512 * 1024;
    private readonly SemaphoreSlim _sending = new(1, 1);

    /// <inheritdoc/>
    public async ValueTask<string?> Receive(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var message = new MemoryStream();
        while (socket.State == WebSocketState.Open)
        {
            ValueWebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                return null;   // the peer dropped without a close
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (message.Length + result.Count > MaxMessageBytes)
            {
                await Close(1009, "message too big", CancellationToken.None).ConfigureAwait(false);
                return null;
            }

            await message.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage && result.MessageType == WebSocketMessageType.Text)
            {
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }

            if (result.EndOfMessage)
            {
                message.SetLength(0);   // a binary message is not in the table: dropped
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask Send(string json, CancellationToken cancellationToken)
    {
        await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State == WebSocketState.Open)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // the peer is gone; the receive loop ends the session
        }
        finally
        {
            _ = _sending.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask Close(int code, string reason, CancellationToken cancellationToken)
    {
        await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync((WebSocketCloseStatus)code, reason, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (WebSocketException)
        {
            // already gone
        }
        finally
        {
            _ = _sending.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _sending.Dispose();
}
