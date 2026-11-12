using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.TerminalSocket;

/// <summary>A fake clock for the socket's timers (hello deadline, ping, pong deadline).</summary>
internal sealed class FakeTime
{
    private readonly FakeTimeProvider _provider = new();

    public TimeProvider Provider => _provider;

    public void Advance(TimeSpan by) => _provider.Advance(by);
}
