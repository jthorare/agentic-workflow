using Microsoft.Extensions.Hosting;

namespace Aw.Sindri.Tests.Server;

/// <summary>A hosted service that refuses to start, as ServeListenerCheck does when no loopback listener was added.</summary>
internal sealed class FailsToStart : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("refused to start");

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
