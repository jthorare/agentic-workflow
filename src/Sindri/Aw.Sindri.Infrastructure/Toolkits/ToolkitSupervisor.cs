using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Toolkits;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>
/// Keeps toolkit processes running (see <see cref="IToolkitRuntime"/>). One watcher task per toolkit waits for its process to end; a crash restarts it after 1 s, then 2 s, then 4 s (doubling), and the
/// third crash inside ten minutes marks the toolkit 'failed' in the store and stops the restarts. A stop and a shutdown end the watcher first, so neither counts as a crash.
/// </summary>
/// <param name="launcher">Starts processes.</param>
/// <param name="store">Takes the 'failed' state.</param>
/// <param name="clock">The clock the backoff and the crash window use.</param>
internal sealed class ToolkitSupervisor(IToolkitProcessLauncher launcher, IToolkitStore store, TimeProvider clock) : IToolkitRuntime, IAsyncDisposable, IDisposable
{
    /// <summary>The crashes that make a toolkit 'failed' when they fall inside <see cref="Window"/>.</summary>
    internal const int MaxCrashes = 3;

    /// <summary>The window the crashes are counted in.</summary>
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Running> _running = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    public async ValueTask<Result<Unit>> Start(Toolkit toolkit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        if (!await Enter(cancellationToken).ConfigureAwait(false))
        {
            return Stopped(toolkit.Name);
        }

        try
        {
            if (_running.TryGetValue(toolkit.Name, out var existing))
            {
                if (!existing.Watcher.IsCompleted)
                {
                    return Unit.Value;   // already up (or restarting): a second start changes nothing
                }

                _ = _running.TryRemove(toolkit.Name, out _);   // its watcher gave up: this start replaces it
                await End(existing).ConfigureAwait(false);
            }

            var launched = await launcher.Launch(toolkit, cancellationToken).ConfigureAwait(false);
            if (launched.IsError)
            {
                return launched.Error;
            }

            var running = new Running(launched.Value, toolkit);
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
        if (!await Enter(cancellationToken).ConfigureAwait(false))
        {
            return Unit.Value;   // shut down: nothing runs
        }

        try
        {
            if (!_running.TryRemove(name, out running))
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

    private static Error Stopped(string name) => SindriErrors.ToolkitNotBuilt.With($"Toolkit '{name}' can't start: Sindri is shutting down");

    /// <summary>Takes the gate; false when the supervisor is already shut down.</summary>
    private async ValueTask<bool> Enter(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return false;
        }

        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;   // shut down while this call waited
        }
    }

    /// <summary>The toolkits that have a live process right now, as 'running'; one waiting out a restart backoff or given up on has none.</summary>
    /// <returns>The toolkits in name order.</returns>
    internal IReadOnlyList<Toolkit> RunningNow() =>
        [.. _running.Values.Where(static r => r.Process is not null).Select(static r => r.Toolkit with { State = ToolkitState.Running }).OrderBy(static t => t.Name, StringComparer.Ordinal)];

    /// <summary>Calls one tool of a running toolkit.</summary>
    /// <param name="name">The toolkit name.</param>
    /// <param name="tool">The tool name.</param>
    /// <param name="argumentsJson">The arguments as a JSON object.</param>
    /// <param name="timeout">How long to wait for the answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tool's answer, SND-TK-001 when the toolkit isn't running, or what the process answers (SND-TK-004, -006, -007, -011).</returns>
    internal ValueTask<Result<string>> Call(string name, string tool, string argumentsJson, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!_running.TryGetValue(name, out var running))
        {
            return ValueTask.FromResult<Result<string>>(SindriErrors.ToolkitNotBuilt.With($"Toolkit '{name}' isn't running (fix: toolkit_start)"));
        }

        return running.Process is { } process
            ? process.Call(tool, argumentsJson, timeout, cancellationToken)
            : ValueTask.FromResult<Result<string>>(SindriErrors.ToolkitProcessDied.With($"Toolkit '{name}' is restarting after a crash (fix: try again in a few seconds)"));
    }

    /// <remarks>A container disposed synchronously: starts the same shutdown <see cref="DisposeAsync"/> runs and leaves it to finish (blocking on it is banned).</remarks>
    public void Dispose() => _ = DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;   // already shut down: a second dispose changes nothing
        }

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
                    await GiveUp(toolkit.Name, running).ConfigureAwait(false);
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

    /// <summary>Drops this watcher's entry, so a later start launches a new process, then marks the toolkit 'failed' on the row as it is now (a rebuild since the start keeps its hash and manifest).</summary>
    private async Task GiveUp(string name, Running running)
    {
        if (await Enter(CancellationToken.None).ConfigureAwait(false))
        {
            try
            {
                _ = _running.TryRemove(new KeyValuePair<string, Running>(name, running));
            }
            finally
            {
                _ = _gate.Release();
            }
        }

        var fresh = await store.Find(name, CancellationToken.None).ConfigureAwait(false);
        if (fresh.IsSuccess && fresh.Value is { } current)
        {
            _ = await store.Save(current with { State = ToolkitState.Failed }, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private sealed class Running(IToolkitProcess first, Toolkit toolkit)
    {
        internal Toolkit Toolkit { get; } = toolkit;

        internal IToolkitProcess? Process { get; set; } = first;

        internal Task Watcher { get; set; } = Task.CompletedTask;

        internal CancellationTokenSource Stopping { get; } = new();
    }
}
