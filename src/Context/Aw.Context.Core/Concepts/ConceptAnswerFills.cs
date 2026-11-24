using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Concepts;

/// <summary>
/// Runs the publish fill of the kept concepts and glossary answers (<see cref="ConceptAnswers.Fill"/>) after the publish, never inside it: the import or promote returns once its
/// version is committed, and the fill runs on its own, one after another, so a publish job ends and serve starts without waiting for a whole-map read. One per process; a fill a
/// restart cuts short leaves the reads to build what it didn't keep. The container's dispose (the host stopping) cancels the fills and waits for them, so none still writes the
/// store after the host has stopped (#4110: a test home's delete lost that race).
/// </summary>
/// <param name="fill">Builds and keeps the answers for a publisher's scope in a scope of its own (the host's registration makes it, so this type needs no container).</param>
/// <param name="failed">Told about a fill that threw (the host logs it), or null.</param>
public sealed class ConceptAnswerFills(Func<ContextScope, CancellationToken, Task> fill, Action<Exception>? failed = null) : IConceptAnswerFills, IAsyncDisposable, IDisposable
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private Task _last = Task.CompletedTask;
    private bool _disposed;

    /// <inheritdoc/>
    public void Start(ContextScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (_gate)
        {
            if (_disposed)
            {
                return;   // the host is stopping: a fill that never starts leaves the reads to build the answers
            }

            // a fill outlives the publish that started it, so it takes no caller's token, only the host's stop; a fill that throws is reported and the next one still runs
            var stop = _stopping.Token;
            _last = _last.ContinueWith(_ => stop.IsCancellationRequested ? Task.CompletedTask : fill(scope, stop), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap()
                .ContinueWith(Report, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
    }

    /// <summary>Cancels the fills without waiting (a container disposed synchronously calls this), then disposes the source.</summary>
    public void Dispose()
    {
        if (Stop(out _))
        {
            _stopping.Cancel();
            // why: safe while a fill still runs: it holds a token that is already cancelled, and Start never reads Token once stopped; Report reads only IsCancellationRequested, which a disposed source still answers
            _stopping.Dispose();
        }
    }

    /// <summary>Cancels the fills and waits for the one running to end.</summary>
    /// <returns>A task that completes then.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!Stop(out var last))
        {
            return;   // registered once, but a second dispose must not wait or cancel again
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _ = await Task.WhenAny(last).ConfigureAwait(false);   // why: waits for the chain without rethrowing; Report already saw how each fill ended
        _stopping.Dispose();
    }

    /// <summary>Marks the fills stopped and hands out the chain to wait for; false when they already were.</summary>
    private bool Stop(out Task last)
    {
        lock (_gate)
        {
            last = _last;
            if (_disposed)
            {
                return false;
            }

            _disposed = true;
            return true;
        }
    }

    private void Report(Task done)
    {
        // why: a fill that fails because the host is stopping is not worth a warning, and the host's logger may already be gone
        if (done.Exception is { } thrown && !_stopping.IsCancellationRequested)
        {
            failed?.Invoke(thrown.GetBaseException());
        }
    }
}
