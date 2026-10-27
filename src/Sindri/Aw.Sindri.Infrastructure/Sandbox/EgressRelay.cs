using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Copies bytes both ways between two streams until either side ends.</summary>
internal static class EgressRelay
{
    /// <summary>How long one direction may keep going after the other ended, unless the caller says otherwise.</summary>
    public static readonly TimeSpan DefaultHalfCloseGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Relays. When one side ends cleanly the other side is half-closed (the sender is done, as an HTTP/1.0 client is after its request) and the
    /// other direction gets <paramref name="halfCloseGrace"/> to finish. A byte-cap stop or a fault cancels both at once.
    /// </summary>
    /// <param name="a">One side.</param>
    /// <param name="b">The other side.</param>
    /// <param name="allow">Called with each chunk's size; false stops the relay (a byte cap).</param>
    /// <param name="halfCloseGrace">How long the second direction may continue after the first ended.</param>
    /// <param name="cancellationToken">Ends the relay.</param>
    /// <returns>A task that ends when the relay does.</returns>
    public static async Task Run(Stream a, Stream b, Func<int, bool> allow, TimeSpan halfCloseGrace, CancellationToken cancellationToken)
    {
        using var both = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var forward = Pump(a, b, allow, both.Token);
        var backward = Pump(b, a, allow, both.Token);
        try
        {
            var first = await Task.WhenAny(forward, backward).ConfigureAwait(false);
            if (first.IsCompletedSuccessfully && !await first.ConfigureAwait(false))
            {
                HalfClose(first == forward ? b : a);
                both.CancelAfter(halfCloseGrace);
            }
            else
            {
                await both.CancelAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            try
            {
                _ = await Task.WhenAll(forward, backward).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
            {
                // Either side closing ends the pair.
            }
        }
    }

    /// <summary>Ends the sending side of a stream: an <see cref="IHalfCloseable"/> or a socket stream. Never throws for a stream that is already closed.</summary>
    /// <param name="stream">The stream.</param>
    public static void HalfClose(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            if (stream is IHalfCloseable seam)
            {
                seam.ShutdownSend();
            }
            else if (stream is NetworkStream socket)
            {
                socket.Socket.Shutdown(SocketShutdown.Send);
            }
        }
        catch (Exception e) when (e is SocketException or ObjectDisposedException or IOException)
        {
            // The peer is already gone; the other pump will see it.
        }
    }

    private static async Task<bool> Pump(Stream from, Stream to, Func<int, bool> allow, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await from.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (!allow(read))
            {
                return true;
            }

            await to.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            await to.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        return false;
    }
}
