using System.Text.Json;
using System.Text;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>
/// One terminal WebSocket (agents spec §15.8, message table in docs/sindri/agents.md): hello within 3 s, ready, whole-screen frames,
/// keys, paste and size in, ping every 15 s with a 10 s pong deadline. A key makes the source capture at once (perf D14).
/// It writes no frame or key to a log or the ledger.
/// </summary>
internal sealed class TerminalSocketSession(ITerminalPaneSource source, Func<string, bool> tokenOk, TimeProvider time)
{
    private const int MaxPasteBytes = 256 * 1024;
    private static readonly TimeSpan _helloWithin = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _pingEvery = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan _pongWithin = TimeSpan.FromSeconds(10);

    /// <summary>Runs the socket until it closes.</summary>
    /// <param name="session">The session.</param>
    /// <param name="channel">The socket.</param>
    /// <param name="cancellationToken">Ends the session (Serve stopping closes with 1001).</param>
    /// <returns>A task that ends when the socket is closed.</returns>
    public async ValueTask Run(SessionRef session, ITerminalChannel channel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);
        try
        {
            await RunSocket(session, channel, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await channel.Close(1001, "Serve is stopping", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static string Ready(TerminalHost host)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("t", "ready");
            json.WriteNumber("v", 1);
            json.WriteStartObject("host");
            json.WriteString("socket", host.Socket);
            json.WriteString("session", host.Session);
            json.WriteString("pane", host.Pane);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string Frame(TerminalFrame frame)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("t", "frame");
            json.WriteNumber("seq", frame.Seq);
            json.WriteStartArray("rows");
            foreach (var row in frame.Rows)
            {
                json.WriteStringValue(row);
            }

            json.WriteEndArray();
            json.WriteStartObject("cursor");
            json.WriteNumber("x", frame.CursorX);
            json.WriteNumber("y", frame.CursorY);
            json.WriteEndObject();
            json.WriteNumber("w", frame.Width);
            json.WriteNumber("h", frame.Height);
            json.WriteNumber("alt", frame.Alt ? 1 : 0);
            json.WriteNumber("inMode", frame.InMode ? 1 : 0);
            json.WriteNumber("dead", frame.Dead ? 1 : 0);
            json.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async ValueTask RunSocket(SessionRef session, ITerminalChannel channel, CancellationToken cancellationToken)
    {
        var hello = await ReadHello(channel, cancellationToken).ConfigureAwait(false);
        if (hello is null)
        {
            return;
        }

        var opened = await source.Open(session, hello.Value, cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            var code = opened.Error.Code;
            await channel.Close(code == SindriErrors.AgentNoPane.Code ? 4015 : 4016, code, cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var pane = opened.Value;
        await channel.Send(Ready(pane.Host), cancellationToken).ConfigureAwait(false);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pong = new SemaphoreSlim(0);
        var background = new[]
        {
            Task.Run(() => PumpFrames(pane, channel, stop), CancellationToken.None),
            Task.Run(() => Ping(channel, pong, stop), CancellationToken.None),
        };
        try
        {
            await ReceiveLoop(pane, channel, pong, stop.Token).ConfigureAwait(false);
        }
        finally
        {
            await stop.CancelAsync().ConfigureAwait(false);
            try
            {
                await Task.WhenAll(background).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // the pump and the pinger end by cancel
            }

            pong.Dispose();
        }
    }

    private async ValueTask<HostSize?> ReadHello(ITerminalChannel channel, CancellationToken cancellationToken)
    {
        using var within = new CancellationTokenSource(_helloWithin, time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, within.Token);
        string? first;
        try
        {
            first = await channel.Receive(either.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (within.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            await channel.Close(1008, "hello did not arrive in 3 s", cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (first is null)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(first);
            var root = doc.RootElement;
            var good = root.TryGetProperty("t", out var t) && t.GetString() == "hello"
                && root.TryGetProperty("token", out var token) && token.GetString() is { } text && tokenOk(text);
            if (!good)
            {
                await channel.Close(1008, SindriErrors.AuthRefused.Code, cancellationToken).ConfigureAwait(false);
                return null;
            }

            return new HostSize(Dimension(root, "cols", 120), Dimension(root, "rows", 36));
        }
        catch (JsonException)
        {
            await channel.Close(1008, SindriErrors.AuthRefused.Code, cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private static int Dimension(JsonElement root, string name, int fallback) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;

    private async Task PumpFrames(IOpenTerminalPane pane, ITerminalChannel channel, CancellationTokenSource stop)
    {
        try
        {
            var dead = false;
            await foreach (var frame in pane.Frames(stop.Token).ConfigureAwait(false))
            {
                await channel.Send(Frame(frame), stop.Token).ConfigureAwait(false);
                dead = frame.Dead;
            }

            if (!dead && !stop.IsCancellationRequested)
            {
                await channel.Close(4016, SindriErrors.AgentPaneGone.Code, CancellationToken.None).ConfigureAwait(false);
                await stop.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // the socket is closing
        }
    }

    private async Task Ping(ITerminalChannel channel, SemaphoreSlim pong, CancellationTokenSource stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_pingEvery, time, stop.Token).ConfigureAwait(false);
                await channel.Send("""{"t":"ping"}""", stop.Token).ConfigureAwait(false);
                try
                {
                    await pong.WaitAsync(_pongWithin, time, stop.Token).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    await channel.Close(1001, "no pong in 10 s", CancellationToken.None).ConfigureAwait(false);
                    await stop.CancelAsync().ConfigureAwait(false);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // the socket is closing
        }
    }

    private static async ValueTask ReceiveLoop(IOpenTerminalPane pane, ITerminalChannel channel, SemaphoreSlim pong, CancellationToken cancellationToken)
    {
        try
        {
            while (await channel.Receive(cancellationToken).ConfigureAwait(false) is { } message)
            {
                if (!await Apply(pane, channel, pong, message, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // the pinger or the pump ended the socket
        }
    }

    /// <summary>Applies one client message. Returns false when the socket was closed for it.</summary>
    private static async ValueTask<bool> Apply(IOpenTerminalPane pane, ITerminalChannel channel, SemaphoreSlim pong, string message, CancellationToken cancellationToken)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(message);
        }
        catch (JsonException)
        {
            return true;   // not a message of the table: dropped
        }

        using (doc)
        {
            var root = doc.RootElement;
            switch (root.TryGetProperty("t", out var t) ? t.GetString() : null)
            {
                case "key" when root.TryGetProperty("key", out var key) && key.GetString() is { } name:
                    _ = await pane.SendKey(name, cancellationToken).ConfigureAwait(false);   // a name outside the list is dropped: no echo
                    break;
                case "key" when root.TryGetProperty("text", out var text) && text.GetString() is { } literal:
                    _ = await pane.SendText(literal, cancellationToken).ConfigureAwait(false);
                    break;
                case "paste" when root.TryGetProperty("text", out var body) && body.GetString() is { } pasted:
                    if (Encoding.UTF8.GetByteCount(pasted) > MaxPasteBytes)
                    {
                        await channel.Close(1009, "paste is over 256 KiB", cancellationToken).ConfigureAwait(false);
                        return false;
                    }

                    _ = await pane.Paste(pasted, cancellationToken).ConfigureAwait(false);
                    break;
                case "size":
                    _ = await pane.Resize(new HostSize(Dimension(root, "cols", 0), Dimension(root, "rows", 0)), cancellationToken).ConfigureAwait(false);
                    break;
                case "pong":
                    _ = pong.Release();
                    break;
                default:
                    break;
            }
        }

        return true;
    }
}
