using System.Net;
using System.Net.Sockets;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>A unix-socket or loopback TCP listener that hands each accepted connection to a handler until it is disposed.</summary>
internal sealed class EgressListener : IAsyncDisposable
{
    private readonly Socket _socket;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _accept;
    private readonly List<Task> _connections = [];
    private readonly Lock _gate = new();
    private int _disposed;

    private EgressListener(Socket socket, int port, Func<Stream, CancellationToken, Task> handler)
    {
        _socket = socket;
        Port = port;
        _accept = Task.Run(() => Accept(handler), _stop.Token);
    }

    /// <summary>Gets the TCP port a loopback listener is bound to; 0 for a unix socket.</summary>
    public int Port { get; }

    /// <summary>Binds <paramref name="path"/> (0600) and starts accepting.</summary>
    /// <param name="path">The socket path.</param>
    /// <param name="handler">Serves one connection.</param>
    /// <returns>The listener.</returns>
    public static EgressListener Start(string path, Func<Stream, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            socket.Bind(new UnixDomainSocketEndPoint(path));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            socket.Listen(64);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        return new EgressListener(socket, 0, handler);
    }

    /// <summary>Binds 127.0.0.1 on a free port and starts accepting: the macOS proxy, where there is no network namespace and the agent connects straight to loopback.</summary>
    /// <param name="handler">Serves one connection.</param>
    /// <returns>The listener; <see cref="Port"/> is the bound port.</returns>
    public static EgressListener StartLoopback(Func<Stream, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            socket.Listen(64);
            return new EgressListener(socket, ((IPEndPoint)socket.LocalEndPoint!).Port, handler);   // why: a bound socket has a local end point
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Stops accepting at once, without waiting: for a start that failed before anything was handed out.</summary>
    public void Abort()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _stop.Cancel();
        _socket.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stop.CancelAsync().ConfigureAwait(false);
        _socket.Dispose();
        _ = await Task.WhenAny(_accept).ConfigureAwait(false);
        Task[] open;
        lock (_gate)
        {
            open = [.. _connections];
        }

        try
        {
            await Task.WhenAll(open).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // A connection that won't end, or that failed, is not the run's fault; it is dropped with the process.
        }

        _stop.Dispose();
    }

    private async Task Accept(Func<Stream, CancellationToken, Task> handler)
    {
        while (!_stop.IsCancellationRequested)
        {
            Socket accepted;
            try
            {
                accepted = await _socket.AcceptAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            var task = Serve(accepted, handler);
            lock (_gate)
            {
                _ = _connections.RemoveAll(t => t.IsCompleted);
                _connections.Add(task);
            }
        }
    }

    private async Task Serve(Socket accepted, Func<Stream, CancellationToken, Task> handler)
    {
        using (accepted)
        {
            var stream = new NetworkStream(accepted, ownsSocket: false);
            await using var _ = stream.ConfigureAwait(false);
            try
            {
                await handler(stream, _stop.Token).ConfigureAwait(false);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
            {
                // A reset or the run's end.
            }
        }
    }
}
