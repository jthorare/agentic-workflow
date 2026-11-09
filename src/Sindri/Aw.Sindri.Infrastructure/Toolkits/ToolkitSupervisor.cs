using Aw.Kernel;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>
/// Keeps toolkit processes running (see <see cref="IToolkitRuntime"/>). One watcher task per toolkit waits for its process to end; a crash restarts it after 1 s, then 2 s, then 4 s (doubling), and the
/// third crash inside ten minutes marks the toolkit 'failed' in the store and stops the restarts. A stop and a shutdown end the watcher first, so neither counts as a crash.
/// </summary>
/// <param name="launcher">Starts processes.</param>
/// <param name="store">Takes the 'failed' state.</param>
/// <param name="clock">The clock the backoff and the crash window use.</param>
internal sealed class ToolkitSupervisor(IToolkitProcessLauncher launcher, IToolkitStore store, TimeProvider clock) : IToolkitRuntime, IAsyncDisposable
{
    /// <summary>The crashes that make a toolkit 'failed' when they fall inside <see cref="Window"/>.</summary>
    internal const int MaxCrashes = 3;

    /// <summary>The window the crashes are counted in.</summary>
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly Dictionary<string, Running> _running = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<Result<Unit>> Start(Toolkit toolkit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_running.ContainsKey(toolkit.Name))
            {
                return Unit.Value;   // already up: a second start changes nothing
            }

            var launched = await launcher.Launch(toolkit, cancellationToken).ConfigureAwait(false);
            if (launched.IsError)
            {
                return launched.Error;
            }

            var running = new Running(launched.Value);
            running.Watcher = Task.Run(() => Watch(toolkit, running), CancellationToken.None);
            _running[toolkit.Name] = running;
            return Unit.Value;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async ValueTask<Result<Unit>> Stop(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        Running? running;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_running.Remove(name, out running))
            {
                return Unit.Value;   // not running is already stopped
            }
        }
        finally
        {
            _ = _gate.Release();
        }

        await End(running).ConfigureAwait(false);
        return Unit.Value;
    }

    public async ValueTask DisposeAsync()
    {
        Running[] all;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            all = [.. _running.Values];
            _running.Clear();
        }
        finally
        {
            _ = _gate.Release();
        }

        foreach (var running in all)
        {
            await End(running).ConfigureAwait(false);
        }

        _gate.Dispose();
    }

    private static async ValueTask End(Running running)
    {
        await running.Stopping.CancelAsync().ConfigureAwait(false);
        await running.Watcher.ConfigureAwait(false);
        if (running.Process is { } process)
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }

        running.Stopping.Dispose();
    }

    private async Task Watch(Toolkit toolkit, Running running)
    {
        var token = running.Stopping.Token;
        Queue<DateTimeOffset> crashes = [];
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (running.Process is { } process)
                {
                    await process.Exited.WaitAsync(token).ConfigureAwait(false);
                    running.Process = null;
                    await process.DisposeAsync().ConfigureAwait(false);
                }

                var now = clock.GetUtcNow();
                crashes.Enqueue(now);
                while (crashes.Peek() < now - Window)
                {
                    _ = crashes.Dequeue();
                }

                if (crashes.Count >= MaxCrashes)
                {
                    _ = await store.Save(toolkit with { State = ToolkitState.Failed }, CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, crashes.Count - 1)), clock, token).ConfigureAwait(false);
                var again = await launcher.Launch(toolkit, token).ConfigureAwait(false);
                running.Process = again.IsError ? null : again.Value;   // a launch that fails counts as the next crash
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // stopped: not a crash
        }
    }

    private sealed class Running(IToolkitProcess first)
    {
        internal IToolkitProcess? Process { get; set; } = first;

        internal Task Watcher { get; set; } = Task.CompletedTask;

        internal CancellationTokenSource Stopping { get; } = new();
    }
}
