using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Testing;

/// <summary>The park handle of a job for tests: every <see cref="WaitFor"/> stays parked until the test resumes or expires it, or the call's token trips.</summary>
public sealed class FakeJobWaiting : IJobWaiting
{
    private readonly Lock _gate = new();
    private readonly List<TaskCompletionSource<bool>> _parked = [];
    private readonly List<(string Signal, TimeSpan Timeout)> _waits = [];

    /// <summary>Every wait started, as (signal, timeout), in order.</summary>
    public IReadOnlyList<(string Signal, TimeSpan Timeout)> Waits { get { lock (_gate) { return [.. _waits]; } } }

    /// <summary>How many waits are parked now.</summary>
    public int Parked { get { lock (_gate) { return _parked.Count; } } }

    /// <inheritdoc/>
    public async ValueTask<bool> WaitFor(string signal, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _waits.Add((signal, timeout));
            _parked.Add(wait);
        }

        try
        {
            await using var registration = cancellationToken.Register(() => wait.TrySetCanceled(cancellationToken));
            return await wait.Task.ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _ = _parked.Remove(wait);
            }
        }
    }

    /// <summary>The signal comes: the oldest parked wait returns true.</summary>
    /// <returns>Whether a wait was parked.</returns>
    public bool Resume() => Complete(signalled: true);

    /// <summary>The time runs out: the oldest parked wait returns false.</summary>
    /// <returns>Whether a wait was parked.</returns>
    public bool Expire() => Complete(signalled: false);

    /// <summary>Waits (polling, bounded by the test's token) until <paramref name="count"/> waits are parked.</summary>
    /// <param name="count">How many.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <returns>A task that completes then.</returns>
    public async Task Until(int count, CancellationToken cancellationToken)
    {
        while (Parked < count)
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool Complete(bool signalled)
    {
        TaskCompletionSource<bool>? oldest;
        lock (_gate)
        {
            oldest = _parked.Count > 0 ? _parked[0] : null;
        }

        return oldest?.TrySetResult(signalled) == true;
    }
}
