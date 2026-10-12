using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Tests.Control;

/// <summary>
/// A minimal loopback HTTP server for the probe: it answers every request with <see cref="Ping"/> as JSON, or, when
/// silent, accepts connections and never answers.
/// </summary>
internal sealed class PingServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<TcpClient> _held = [];
    private readonly Task _loop;

    public PingServer(Ping? answer)
    {
        _listener.Start();
        _loop = Serve(answer);
    }

    public Uri Url => new(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}"));

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // stopped
        }

        _held.ForEach(c => c.Dispose());
        _stop.Dispose();
    }

    private async Task Serve(Ping? answer)
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException)
            {
                return;
            }

            if (answer is null)
            {
                _held.Add(client);   // silent: hold the connection open, never answer
                continue;
            }

            using (client)
            {
                await Answer(client.GetStream(), answer);
            }
        }
    }

    private async Task Answer(NetworkStream stream, Ping answer)
    {
        var buffer = new byte[4096];
        _ = await stream.ReadAsync(buffer, _stop.Token);   // the request; its content doesn't matter here
        var body = AwJson.Serialize(answer, SliceHarness.Json);
        var head = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture,
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n"));
        await stream.WriteAsync(head, _stop.Token);
        await stream.WriteAsync(body, _stop.Token);
    }
}
