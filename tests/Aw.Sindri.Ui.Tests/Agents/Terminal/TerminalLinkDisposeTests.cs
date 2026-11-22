using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>A send still in flight when the link is disposed ends as "not sent", never as an exception (#3860).</summary>
[Lane(TestLane.Integration)]
public sealed class TerminalLinkDisposeTests
{
    private const int HugeMessageChars = 32 * 1024 * 1024;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_send_in_flight_when_the_link_is_disposed_returns_false()
    {
        var port = FreePort();
        var api = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{port}/"));
        var listener = new HttpListener();
        try
        {
            listener.Prefixes.Add(api.AbsoluteUri);
            listener.Start();
            using var serverEnds = CancellationTokenSource.CreateLinkedTokenSource(Token);
#pragma warning disable CA2025 // the test awaits `server` below, before the finally closes the listener
            var server = HoldOpen(listener, serverEnds.Token);
#pragma warning restore CA2025
            var connector = new WebSocketTerminalConnector(new InMemoryHumanToken("token"));
            var link = await connector.Connect(new TerminalOpen(api, "claude-code", "ideas", 120, 36), Token);
            Assert.NotNull(link);

            // The server never reads, so a message this big fills the socket's buffers and the send waits inside the socket.
            var sending = link.Send(new string('x', HugeMessageChars), Token);
            await Task.Delay(TimeSpan.FromMilliseconds(300), Token);
            await link.DisposeAsync();
            var sent = await sending;
            await serverEnds.CancelAsync();
            await server;

            Assert.False(sent);
        }
        finally
        {
            listener.Close();
        }
    }

    // Accepts the one socket and leaves it unread until the test ends.
    private static async Task HoldOpen(HttpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            var accepted = await context.AcceptWebSocketAsync(subProtocol: null);
            using var socket = accepted.WebSocket;
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (Exception e) when (e is OperationCanceledException or HttpListenerException or WebSocketException or ObjectDisposedException)
        {
            // the test ended
        }
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}
