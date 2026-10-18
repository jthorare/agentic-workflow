using System.Globalization;
using System.Text;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The allowlisting proxy: plain HTTP (absolute URI) and CONNECT, no TLS interception.</summary>
/// <param name="policy">The policy.</param>
/// <param name="dialer">Opens outbound connections.</param>
/// <param name="log">The egress.log.</param>
/// <param name="caps">The per-run caps.</param>
internal sealed class EgressProxy(EgressPolicy policy, IEgressDialer dialer, EgressLog log, EgressCaps caps)
{
    private const int MaxHead = 16 * 1024;
    private static readonly byte[] _headEnd = "\r\n\r\n"u8.ToArray();
    private int _active;
    private long _bytes;

    /// <summary>Serves one client connection.</summary>
    /// <param name="client">The client's stream.</param>
    /// <param name="cancellationToken">Ends the connection.</param>
    /// <returns>A task that ends with the connection.</returns>
    public async Task Handle(Stream client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (Interlocked.Increment(ref _active) > caps.MaxConnections || Interlocked.Read(ref _bytes) >= caps.MaxBytes)
        {
            log.Record("(cap)", 0, EgressVerdict.Refused);
            await Reply(client, 503, "Service Unavailable", cancellationToken).ConfigureAwait(false);
            _ = Interlocked.Decrement(ref _active);
            return;
        }

        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(caps.ConnectionLifetime);
            await Serve(client, limit.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or InvalidDataException)
        {
            // A reset, a cap or the run's end closes the connection; there's nothing to report.
        }
        finally
        {
            _ = Interlocked.Decrement(ref _active);
        }
    }

    private async Task Serve(Stream client, CancellationToken cancellationToken)
    {
        using var headLimit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        headLimit.CancelAfter(caps.HeadTimeout);
        var (head, tail) = await ReadHead(client, headLimit.Token).ConfigureAwait(false);
        var lines = Encoding.Latin1.GetString(head).Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length != 3 || !TryTarget(parts, out var host, out var port, out var origin))
        {
            await Reply(client, 400, "Bad Request", cancellationToken).ConfigureAwait(false);
            return;
        }

        var decision = await policy.Decide(host, port, cancellationToken).ConfigureAwait(false);
        if (decision is not { Verdict: EgressVerdict.Allowed, Address: { } address })
        {
            log.Record(host, port, decision.Verdict);
            await Reply(client, 403, "Forbidden", cancellationToken).ConfigureAwait(false);
            return;
        }

        var connect = string.Equals(parts[0], "CONNECT", StringComparison.OrdinalIgnoreCase);
        var upstream = await TryDial(client, address, port, cancellationToken).ConfigureAwait(false);
        log.Record(host, port, upstream is null ? EgressVerdict.Failed : EgressVerdict.Allowed);
        if (upstream is null)
        {
            return;
        }

        await using (upstream.ConfigureAwait(false))
        {
            var first = connect ? Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n") : Rewrite(parts, lines, origin ?? "/");
            await (connect ? client : upstream).WriteAsync(first, cancellationToken).ConfigureAwait(false);
            if (tail.Length > 0)
            {
                await upstream.WriteAsync(tail, cancellationToken).ConfigureAwait(false);
            }

            await EgressRelay.Run(client, upstream, c => Interlocked.Add(ref _bytes, c) <= caps.MaxBytes, caps.HalfCloseGrace, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Stream?> TryDial(Stream client, System.Net.IPAddress address, int port, CancellationToken cancellationToken)
    {
        try
        {
            return await dialer.Dial(address, port, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or System.Net.Sockets.SocketException)
        {
            await Reply(client, 502, "Bad Gateway", cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private static byte[] Rewrite(string[] parts, string[] lines, string origin)
    {
        var text = new StringBuilder().Append(parts[0]).Append(' ').Append(origin).Append(' ').Append(parts[2]).Append("\r\n");
        foreach (var line in lines.Skip(1).Where(l => l.Length > 0 && !l.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase) && !l.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase)))
        {
            _ = text.Append(line).Append("\r\n");
        }

        return Encoding.Latin1.GetBytes(text.Append("Connection: close\r\n\r\n").ToString());
    }

    private static bool TryTarget(string[] parts, out string host, out int port, out string? origin)
    {
        host = string.Empty;
        port = 0;
        origin = null;
        if (string.Equals(parts[0], "CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            return HostPort(parts[1], 443, out host, out port);
        }

        if (!Uri.TryCreate(parts[1], UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        host = uri.Host;
        port = uri.Port;
        origin = uri.PathAndQuery;
        return host.Length > 0;
    }

    private static bool HostPort(string text, int defaultPort, out string host, out int port)
    {
        var colon = text.LastIndexOf(':');
        var bracketed = text.StartsWith('[');
        if (colon > 0 && (!bracketed || text[colon - 1] == ']'))
        {
            host = text[..colon].Trim('[', ']');
            return int.TryParse(text[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out port) && host.Length > 0;
        }

        host = text.Trim('[', ']');
        port = defaultPort;
        return host.Length > 0;
    }

    private static async Task<(byte[] Head, byte[] Tail)> ReadHead(Stream client, CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxHead];
        var length = 0;
        while (length < MaxHead)
        {
            var read = await client.ReadAsync(buffer.AsMemory(length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("The client closed before it finished the request.");
            }

            length += read;
            var end = buffer.AsSpan(0, length).IndexOf(_headEnd);
            if (end >= 0)
            {
                return (buffer[..end], buffer[(end + 4)..length]);
            }
        }

        throw new InvalidDataException("The request head is too long.");
    }

    private static async Task Reply(Stream client, int status, string text, CancellationToken cancellationToken)
    {
        try
        {
            await client.WriteAsync(Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {status} {text}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")), cancellationToken).ConfigureAwait(false);
            await client.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The client is gone; the verdict is already counted.
        }
    }
}
