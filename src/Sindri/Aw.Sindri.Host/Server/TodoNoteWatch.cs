using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Todos;

namespace Aw.Sindri.Host.Server;

/// <summary>
/// The todo note watch (todo spec §4.5 "Reading"): while the process holds the lease it runs a note pass at start and then every 60 s, as the caller 'service:todo-note'. A pass also runs when the note's file changes
/// (a non-recursive folder watch, a trigger only), after any todo event whose 'by' isn't 'obsidian' or 'service:todo-note', and when a pick's Undo window closes. One pass at a time. With no 'todo.obsidian' key the
/// pass stops the sync (it frees the base files) and the folder watch is dropped; the 60 s check picks the key up again.
/// </summary>
/// <param name="passes">Runs a pass.</param>
/// <param name="folder">Watches the note's folder.</param>
/// <param name="tail">Follows the ledger.</param>
/// <param name="picks">The picks still inside their Undo window.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="clock">The clock.</param>
/// <param name="log">Where a failed cycle is reported.</param>
internal sealed partial class TodoNoteWatch(NotePasses passes, NoteFolder folder, TodoEventTail tail, TodoPickWindow picks, HeldLease held, TimeProvider clock, ILogger<TodoNoteWatch> log) : BackgroundService
{
    /// <summary>How often a pass runs while the lease is held.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan _idle = TimeSpan.FromSeconds(1);

    private readonly SemaphoreSlim _poke = new(0, 1);

    public override void Dispose()
    {
        folder.Dispose();
        _poke.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (held.Current is null)
                {
                    folder.Drop();
                    await Task.Delay(_idle, clock, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await Cycle(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)   // boundary: a broken cycle must not stop the watch; the next one tries again
            {
                LogCycleFailed(log, e.GetType().Name, e.Message);
                await Pause(stoppingToken).ConfigureAwait(false);
            }
        }

        folder.Drop();
    }

    private async Task Cycle(CancellationToken ct)
    {
        await tail.Start(ct).ConfigureAwait(false);
        await folder.Follow(Poke, ct).ConfigureAwait(false);
        _ = _poke.Wait(0, CancellationToken.None);   // a change during the pass leaves a new poke
        await passes.Run(ct).ConfigureAwait(false);
        await WaitForReason(Deadline(), ct).ConfigureAwait(false);
    }

    private DateTimeOffset Deadline()
    {
        var now = clock.GetUtcNow();
        var deadline = now + Every;
        var (todos, hold) = picks.Open(now);
        foreach (var at in todos.Select(id => picks.Find(id, now)?.At).Append(hold ? picks.FindHold(now)?.At : null).OfType<DateTimeOffset>())
        {
            var closes = TodoPickWindow.Until(at);
            deadline = closes < deadline ? closes : deadline;   // the writer leaves a picked line alone until its window closes
        }

        return deadline;
    }

    private void Poke()
    {
        try
        {
            _ = _poke.Release();
        }
        catch (SemaphoreFullException)
        {
            // a poke is already waiting
        }
        catch (ObjectDisposedException)
        {
            // shutting down
        }
    }

    /// <summary>Waits until <paramref name="deadline"/>, a file change, or a todo event somebody else wrote.</summary>
    private async Task WaitForReason(DateTimeOffset deadline, CancellationToken ct)
    {
        while (true)
        {
            var left = deadline - clock.GetUtcNow();
            if (left <= TimeSpan.Zero)
            {
                return;
            }

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var timer = Task.Delay(left, clock, stop.Token);
            var poke = _poke.WaitAsync(stop.Token);
            var events = tail.Wait(left, stop.Token);
            var first = await Task.WhenAny(timer, poke, events).ConfigureAwait(false);
            await stop.CancelAsync().ConfigureAwait(false);
            await Settle(timer, poke, events).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (first != events || await tail.Foreign(ct).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private static async Task Settle(params Task[] tasks)
    {
        foreach (var task in tasks)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // the wait that lost
            }
        }
    }

    private async Task Pause(CancellationToken ct)
    {
        try
        {
            await Task.Delay(_idle, clock, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The todo note watch's cycle threw {Type}: {Detail}")]
    private static partial void LogCycleFailed(ILogger logger, string type, string detail);
}
