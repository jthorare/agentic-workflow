namespace Aw.Sindri.Host.Server;

/// <summary>
/// Fails serve's host closed: before any hosted service starts (Kestrel included) it checks that
/// <see cref="ServePort.Listen"/> chose the port and added the loopback listener. Without that listener Kestrel would bind
/// its default, localhost:5000, so the host refuses to start instead.
/// </summary>
internal sealed class ServeListenerCheck(ServePort port) : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken) => port.Chosen is null
        ? throw new InvalidOperationException("serve's host started before ServePort.Listen chose its port; it binds nothing else")
        : Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
