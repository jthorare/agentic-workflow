using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// Builds the code index's <see cref="QueuedClock"/> as serve starts and seeds it with the repos the profile names then, so a repo already queued
/// counts its queued time from serve start, not from the first status read (#3480). A repo added later counts from its first read.
/// </summary>
internal sealed class QueuedClockStart(QueuedClock clock, IServiceScopeFactory scopes) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var current = await scope.ServiceProvider.GetRequiredService<CodeIndexScopes>().Current(cancellationToken).ConfigureAwait(false);
            if (current.IsSuccess)   // why: no profile yet means no repo to seed; every repo then counts from its first read
            {
                clock.Seed(current.Value.Repos.Select(static r => r.Name.Value));
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
