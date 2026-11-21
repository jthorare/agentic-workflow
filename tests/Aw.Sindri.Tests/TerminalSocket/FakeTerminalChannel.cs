using System.Threading.Channels;
using Aw.Sindri.Features.TerminalSocket;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>A terminal channel fed with the client's messages; it records what the session sends and how it closes.</summary>
internal sealed class FakeTerminalChannel : ITerminalChannel
{
    private readonly Channel<string?> _incoming = Channel.CreateUnbounded<string?>();
    private readonly List<string> _sent = [];

    public FakeTerminalChannel(params string[] messages)
    {
        foreach (var message in messages)
        {
            _ = _incoming.Writer.TryWrite(message);
        }
    }

    public IReadOnlyList<string> Sent
    {
        get
        {
            lock (_sent)
            {
                return [.. _sent];
            }
        }
    }

    public (int Code, string Reason)? Closed { get; private set; }

    /// <summary>The client closes its side.</summary>
    public void End() => _incoming.Writer.TryComplete();

    public async Task WaitForSent(int count, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(30));   // a guard against a hang, not a race: a loaded unit lane is slow
        while (Sent.Count < count)
        {
            await Task.Delay(5, limit.Token);
        }
    }

    public async ValueTask<string?> Receive(CancellationToken cancellationToken) =>
        await _incoming.Reader.WaitToReadAsync(cancellationToken) && _incoming.Reader.TryRead(out var message) ? message : null;

    public ValueTask Send(string json, CancellationToken cancellationToken)
    {
        lock (_sent)
        {
            _sent.Add(json);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask Close(int code, string reason, CancellationToken cancellationToken)
    {
        Closed = (code, reason);
        return ValueTask.CompletedTask;
    }
}
