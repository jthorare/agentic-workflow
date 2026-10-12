using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Testing;

/// <summary>The shutdown port as a flag and a token.</summary>
public sealed class FakeShutdown : IShutdown, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>Whether a stop was requested.</summary>
    public bool Requested => _stopping.IsCancellationRequested;

    /// <inheritdoc/>
    public CancellationToken Stopping => _stopping.Token;

    /// <inheritdoc/>
    public void Request() => _stopping.Cancel();

    /// <inheritdoc/>
    public void Dispose() => _stopping.Dispose();
}
