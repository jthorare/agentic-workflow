using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Builds the code index's <see cref="QueuedClock"/> as serve starts, so a repo already queued then counts its queued time from
/// serve start, not from the first status read (#3480). The clock is a lazy singleton; asking for it here is the whole job.
/// </summary>
internal sealed class QueuedClockStart(QueuedClock clock) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = clock;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
