using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.TerminalSocket;

/// <summary>
/// One terminal WebSocket (agents spec §15.8, message table in docs/sindri/agents.md): hello within 3 s, ready, whole-screen frames,
/// keys, paste and size in, ping every 15 s with a 10 s pong deadline. A key makes the source capture at once (perf D14).
/// It writes no frame or key to a log or the ledger.
/// </summary>
// why: internal; the Host's TerminalSocketEndpoint builds it through InternalsVisibleTo, and MA0182 only looks inside this assembly
#pragma warning disable MA0182
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

    private async ValueTask RunSocket(SessionRef session, ITerminalChannel channel, CancellationToken cancellationToken)
    {
        var hello = await ReadHello(channel, cancellationToken).ConfigureAwait(false);
        if (hello is null)
        {
            return;
        }

        Result<IOpenTerminalPane> opened;
        try
        {
            opened = await source.Open(session, hello, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            opened = SindriErrors.AgentPaneGone.With("the pane source timed out");   // a call budget fired, not the caller: close with a code instead of throwing out of the endpoint
        }

        if (opened.IsError)
        {
            var code = opened.Error.Code;
            await channel.Close(code == SindriErrors.AgentNoPane.Code ? 4015 : 4016, code, cancellationToken).ConfigureAwait(false);
            return;
        }

        var pane = opened.Value;
        await using (pane.ConfigureAwait(false))
        {
            await channel.Send(TerminalSocketText.Ready(pane.Host), cancellationToken).ConfigureAwait(false);
            await Drive(pane, channel, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Pumps frames, pings and applies client messages until one of the three ends the socket.</summary>
    private async ValueTask Drive(IOpenTerminalPane pane, ITerminalChannel channel, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var pong = new SemaphoreSlim(0);
#pragma warning disable CA2025 // both tasks are awaited in the finally below (Task.WhenAll) before stop and pong are disposed
        var background = new[]
        {
            Task.Run(() => PumpFrames(pane, channel, stop), CancellationToken.None),
            Task.Run(() => Ping(channel, pong, stop), CancellationToken.None),
        };
#pragma warning restore CA2025
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

    private static async Task PumpFrames(IOpenTerminalPane pane, ITerminalChannel channel, CancellationTokenSource stop)
    {
        try
        {
            var dead = false;
            await foreach (var frame in pane.Frames(stop.Token).ConfigureAwait(false))
            {
                await channel.Send(TerminalSocketText.Frame(frame), stop.Token).ConfigureAwait(false);
                dead = frame.Dead;
            }

            if (!dead && !stop.IsCancellationRequested)
            {
                await channel.Close(4016, SindriErrors.AgentPaneGone.Code, CancellationToken.None).ConfigureAwait(false);
                await stop.CancelAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        {
            // The source's own call budget fired, not the socket closing: the screen would freeze while pings keep passing, so close with a code.
            await channel.Close(4016, SindriErrors.AgentPaneGone.Code, CancellationToken.None).ConfigureAwait(false);
            await stop.CancelAsync().ConfigureAwait(false);
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
                    await pong.WaitAsync(stop.Token).WaitAsync(_pongWithin, time, stop.Token).ConfigureAwait(false);
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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A send hit the source's call budget while the socket was still open: close with a code, not silently.
            await channel.Close(4016, SindriErrors.AgentPaneGone.Code, CancellationToken.None).ConfigureAwait(false);
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
            var kind = root.TryGetProperty("t", out var t) ? t.GetString() : null;
            if (string.Equals(kind, "paste", StringComparison.Ordinal) && root.TryGetProperty("text", out var body) && body.GetString() is { } pasted)
            {
                return await Paste(pane, channel, pasted, cancellationToken).ConfigureAwait(false);
            }

            await Typed(pane, pong, kind, root, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static async ValueTask Typed(IOpenTerminalPane pane, SemaphoreSlim pong, string? kind, JsonElement root, CancellationToken cancellationToken)
    {
        switch (kind)
        {
            case "key" when root.TryGetProperty("key", out var key) && key.GetString() is { } name:
                _ = await pane.SendKey(name, cancellationToken).ConfigureAwait(false);   // a name outside the list is dropped: no echo
                break;
            case "key" when root.TryGetProperty("text", out var text) && text.GetString() is { } literal:
                _ = await pane.SendText(literal, cancellationToken).ConfigureAwait(false);
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

    private static async ValueTask<bool> Paste(IOpenTerminalPane pane, ITerminalChannel channel, string pasted, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetByteCount(pasted) > MaxPasteBytes)
        {
            await channel.Close(1009, "paste is over 256 KiB", cancellationToken).ConfigureAwait(false);
            return false;
        }

        _ = await pane.Paste(pasted, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
#pragma warning restore MA0182
