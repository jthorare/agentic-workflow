using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Views;

/// <summary>
/// The projector's progress in memory: the projector publishes its status here, queries wait on it and RebuildViews
/// requests a rebuild through it. Waits time out on <paramref name="clock"/>; each publish pulses the waiters.
/// </summary>
/// <param name="clock">The only clock.</param>
internal sealed class ProjectorProgress(TimeProvider clock) : IProjectorStatus, IProjectorControl, IProjectorFeed
{
    private readonly Lock _gate = new();
    // starting: no pass has published yet, so the status must not read Healthy (#2053)
    private ViewsStatus _current = new(0, 0, ProjectedAt: null, Rebuilding: false, Stalled: false, StalledAt: null, StalledType: null, Starting: true);
    private TaskCompletionSource _pulse = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _rebuildRequested;

    /// <inheritdoc/>
    public ViewsStatus Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <inheritdoc/>
    public void Publish(ViewsStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        TaskCompletionSource pulse;
        lock (_gate)
        {
            // the head is read before the cursor moves, so mid-rebuild it can lag it ("cursor 66 of 65"): never show it behind
            var shown = status.LedgerHead < status.Cursor ? status with { LedgerHead = status.Cursor } : status;
            _current = _rebuildRequested ? shown with { Rebuilding = true } : shown;
            pulse = _pulse;
            _pulse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        _ = pulse.TrySetResult();
    }

    /// <inheritdoc/>
    public bool RebuildRequested
    {
        get
        {
            lock (_gate)
            {
                return _rebuildRequested;
            }
        }
    }

    /// <inheritdoc/>
    public bool TakeRebuildRequest()
    {
        lock (_gate)
        {
            var requested = _rebuildRequested;
            _rebuildRequested = false;
            return requested;
        }
    }

    /// <inheritdoc/>
    public void RequestRebuild()
    {
        lock (_gate)
        {
            _rebuildRequested = true;
            _current = _current with { Rebuilding = true };
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> WaitFor(long sequence, TimeSpan limit, CancellationToken cancellationToken)
    {
        var deadline = clock.GetUtcNow() + limit;
        while (true)
        {
            ViewsStatus status;
            Task pulse;
            lock (_gate)
            {
                (status, pulse) = (_current, _pulse.Task);
            }

            var remaining = deadline - clock.GetUtcNow();
            if (status.Cursor >= sequence || status.Stalled || remaining <= TimeSpan.Zero)
            {
                return status.Cursor >= sequence ? Unit.Value : Behind(status, sequence);
            }

            await Pulse(pulse, remaining, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task Pulse(Task pulse, TimeSpan remaining, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeout = Task.Delay(remaining, clock, stop.Token);
        _ = await Task.WhenAny(pulse, timeout).ConfigureAwait(false);
        await stop.CancelAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static Error Behind(ViewsStatus status, long sequence) => SindriErrors.ViewsBehind.With(status.Stalled
        ? string.Create(CultureInfo.InvariantCulture,
            $"views {status.StalledPhrase()} with the cursor at {status.Cursor}, waiting for {sequence}: run {status.StalledFix()}")
        : string.Create(CultureInfo.InvariantCulture, $"views at cursor {status.Cursor}, waiting for {sequence}"));
}
