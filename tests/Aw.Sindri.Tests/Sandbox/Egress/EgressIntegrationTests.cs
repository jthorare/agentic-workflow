using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Plan;
using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

/// <summary>The real listeners on unix sockets in a temp directory, with loopback stubs for Sindri and the allowed host.</summary>
[Collection(EgressProcesses.Name)]
public sealed class EgressIntegrationTests : IAsyncDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("egress-it").FullName;
    private readonly List<IAsyncDisposable> _cleanup = [];

    public async ValueTask DisposeAsync()
    {
        foreach (var item in _cleanup)
        {
            await item.DisposeAsync();
        }

        Directory.Delete(_root, recursive: true);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Sindri_mcp_call_goes_through_the_forward_not_the_proxy()
    {
        var stub = StubServer.Start("mcp-ok");
        _cleanup.Add(stub);
        var running = Start(stub.Port, [], new RedirectingDialer(0));

        var viaForward = await Http(Path.Combine(running.Directory, "sindri.sock"), $"POST /sindri/mcp HTTP/1.1\r\nHost: 127.0.0.1:{Num(stub.Port)}\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
        var viaProxy = await Http(Path.Combine(running.Directory, "proxy.sock"), $"POST http://127.0.0.1:{Num(stub.Port)}/sindri/mcp HTTP/1.1\r\nHost: 127.0.0.1:{Num(stub.Port)}\r\nContent-Length: 0\r\n\r\n");
        await running.DisposeAsync();

        Assert.EndsWith("mcp-ok", viaForward, StringComparison.Ordinal);
        Assert.Equal(1, stub.Requests);
        Assert.StartsWith("HTTP/1.1 403", viaProxy, StringComparison.Ordinal);
        var lines = await File.ReadAllLinesAsync(Path.Combine(_root, "run", "egress.log"), TestContext.Current.CancellationToken);
        Assert.Contains(lines, l => string.Equals(l, Num(stub.Port, "127.0.0.1\t{0}\tdenied\t1"), StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("allowed", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_client_that_half_closes_after_its_request_still_gets_the_answer()
    {
        var stub = StubServer.Start("mcp-ok");
        _cleanup.Add(stub);
        var running = Start(stub.Port, [], new RedirectingDialer(0));
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(running.Directory, "sindri.sock")), TestContext.Current.CancellationToken);

        _ = await socket.SendAsync("GET /sindri/mcp HTTP/1.0\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken);
        socket.Shutdown(SocketShutdown.Send);
        var answer = new StringBuilder();
        var buffer = new byte[1024];
        int read;
        while ((read = await socket.ReceiveAsync(buffer, TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)) > 0)
        {
            _ = answer.Append(Encoding.ASCII.GetString(buffer, 0, read));
        }

        await running.DisposeAsync();

        Assert.EndsWith("mcp-ok", answer.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Real_proxy_relays_to_an_allowed_host_and_refuses_another()
    {
        var upstream = StubServer.Start("allowed-ok");
        _cleanup.Add(upstream);
        var running = Start(1, [new PlanGroup("repo", ["127.0.0.1"])], new RedirectingDialer(upstream.Port));
        var proxySocket = Path.Combine(running.Directory, "proxy.sock");

        var allowed = await Http(proxySocket, "GET http://127.0.0.1/ HTTP/1.1\r\nHost: 127.0.0.1\r\n\r\n");
        var denied = await Http(proxySocket, "CONNECT other.example:443 HTTP/1.1\r\n\r\n");
        await running.DisposeAsync();

        Assert.EndsWith("allowed-ok", allowed, StringComparison.Ordinal);
        Assert.StartsWith("HTTP/1.1 403", denied, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Socat_forward_chain_carries_a_call_to_sindri()
    {
        var socat = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(d => Path.Combine(d, "socat")).FirstOrDefault(File.Exists);
        Assert.SkipWhen(socat is null, "socat is not installed");
        var stub = StubServer.Start("mcp-ok");
        _cleanup.Add(stub);
        var running = Start(stub.Port, [], new RedirectingDialer(0), socat);
        var free = new TcpListener(IPAddress.Loopback, 0);
        free.Start();
        var port = ((IPEndPoint)free.LocalEndpoint).Port;
        free.Stop();

        using var forwarder = Process.Start(new ProcessStartInfo(socat!, [$"TCP-LISTEN:{Num(port)},bind=127.0.0.1,fork,reuseaddr", "UNIX-CONNECT:" + Path.Combine(running.Directory, "sindri.sock")]) { UseShellExecute = false })!;
        try
        {
            using var http = new HttpClient(new HttpClientHandler { UseProxy = false });
            var body = string.Empty;
            for (var attempt = 0; attempt < 50 && body.Length == 0; attempt++)
            {
                try
                {
                    body = await http.GetStringAsync($"http://127.0.0.1:{Num(port)}/sindri/mcp", TestContext.Current.CancellationToken);
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100, TestContext.Current.CancellationToken);
                }
            }

            Assert.Equal("mcp-ok", body);
        }
        finally
        {
            forwarder.Kill();
            await forwarder.WaitForExitAsync(CancellationToken.None);
            await running.DisposeAsync();
        }
    }

    private RunningEgress Start(int sindriPort, IReadOnlyList<PlanGroup> groups, IEgressDialer dialer, string? socat = null)
    {
        var fake = socat ?? Path.Combine(_root, "socat");
        if (socat is null)
        {
            File.WriteAllText(fake, "#!/bin/sh\n");
        }

        var service = new EgressService(new FakeEgressResolver([]), dialer, n => n == SandboxEnvironment.Socat ? fake : null, Path.Combine(_root, "run"));
        var result = service.Start(new PlanNetwork(NetworkMode.Enforce, groups, sindriPort, 3128, Path.Combine(_root, "n")), EgressCaps.Default);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        _cleanup.Add(result.Value);
        return result.Value;
    }

    private static string Num(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Num(int value, string format) => string.Format(System.Globalization.CultureInfo.InvariantCulture, format, value);

    private static async Task<string> Http(string socketPath, string request)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), TestContext.Current.CancellationToken);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream, Encoding.ASCII);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private sealed class RedirectingDialer(int port) : IEgressDialer
    {
        public async ValueTask<Stream> Dial(IPAddress address, int ignored, CancellationToken cancellationToken)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
    }

    private sealed class StubServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _loop;
        private int _requests;

        private StubServer(string body)
        {
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(() => Serve(body), _stop.Token);
        }

        public int Port { get; }

        public int Requests => Volatile.Read(ref _requests);

        public static StubServer Start(string body) => new(body);

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            _listener.Stop();
            try
            {
                await _loop;
            }
            catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
            {
            }

            _stop.Dispose();
        }

        private async Task Serve(string body)
        {
            while (!_stop.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer, _stop.Token);
                if (read == 0)
                {
                    continue;
                }

                _ = Interlocked.Increment(ref _requests);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Length: {Num(body.Length)}\r\nConnection: close\r\n\r\n{body}"), _stop.Token);
            }
        }
    }
}
