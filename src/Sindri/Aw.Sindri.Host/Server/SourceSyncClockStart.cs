using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Server;

/// <summary>Builds the context source look's <see cref="SourceSyncClock"/> as serve starts, so a source's first-pass limit counts from serve start, not from the first status read (#3832).</summary>
internal sealed class SourceSyncClockStart(SourceSyncClock clock) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = clock.Running();   // why: the host resolves this service, and so the clock, as serve starts; nothing else to do
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
