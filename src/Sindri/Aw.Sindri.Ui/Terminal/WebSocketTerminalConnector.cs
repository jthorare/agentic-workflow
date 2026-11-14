using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Terminal;

/// <summary>
/// The real connector: a <see cref="ClientWebSocket"/> to GET /sindri/sessions/{provider}/{id}/terminal. The human token goes in the first message (hello), never in the URL.
/// A refusal before the upgrade is read as its close code: 409 SND-AGENT-015 as 4015, 404 SND-AGENT-016 as 4016; any other failure is null (a drop).
/// </summary>
internal sealed class WebSocketTerminalConnector(IHumanTokenSource token) : ITerminalConnector
{
    /// <inheritdoc />
    public async Task<ITerminalLink?> Connect(Uri api, string provider, string id, int cols, int rows, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        try
        {
            var scheme = string.Equals(api.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            var path = $"/sindri/sessions/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(id)}/terminal";
            await socket.ConnectAsync(new UriBuilder(api) { Scheme = scheme, Path = path }.Uri, cancellationToken).ConfigureAwait(false);
            var hello = TerminalSocketMessages.Hello(await token.Read(cancellationToken).ConfigureAwait(false) ?? string.Empty, cols, rows);
            await socket.SendAsync(Encoding.UTF8.GetBytes(hello), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
            return new Link(socket);
        }
        catch (WebSocketException)
        {
            var status = socket.HttpStatusCode;
            socket.Dispose();
            return Refused(status);
        }
        catch (Exception e) when (e is IOException or HttpRequestException or UnauthorizedAccessException)
        {
            socket.Dispose();
            return null;
        }
    }

    private static ClosedLink? Refused(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Conflict => new ClosedLink(4015),
        HttpStatusCode.NotFound => new ClosedLink(4016),
        _ => null,
    };

    private sealed class ClosedLink(int code) : ITerminalLink
    {
        public int? CloseCode { get; } = code;

#pragma warning disable CS1998 // an iterator with nothing to yield
        public async IAsyncEnumerable<TerminalServerMessage> Read([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield break;
        }
#pragma warning restore CS1998

        public Task<bool> Send(string json, CancellationToken cancellationToken) => Task.FromResult(false);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class Link(ClientWebSocket socket) : ITerminalLink
    {
        private readonly SemaphoreSlim _sending = new(1, 1);

        public int? CloseCode { get; private set; }

        public async IAsyncEnumerable<TerminalServerMessage> Read([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            using var text = new MemoryStream();
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    CloseCode = (int?)result.CloseStatus ?? 1005;
                    yield break;
                }

                text.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    var json = Encoding.UTF8.GetString(text.GetBuffer(), 0, (int)text.Length);
                    text.SetLength(0);
                    yield return TerminalSocketMessages.Parse(json);
                }
            }

            CloseCode ??= (int?)socket.CloseStatus ?? 1006;
        }

        public async Task<bool> Send(string json, CancellationToken cancellationToken)
        {
            if (socket.State != WebSocketState.Open)
            {
                return false;
            }

            await _sending.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (WebSocketException)
            {
                return false;
            }
            finally
            {
                _ = _sending.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            socket.Dispose();
            _sending.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
