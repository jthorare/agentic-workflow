using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Host.Server;

/// <summary>The shutdown port: a token the serve loop watches. Stop requests and a lost lease trip it.</summary>
internal sealed class ProcessShutdown : IShutdown, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>Cancelled once a stop is requested.</summary>
    public CancellationToken Stopping => _stopping.Token;

    public void Request() => _stopping.Cancel();

    public void Dispose() => _stopping.Dispose();
}
