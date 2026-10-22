using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The single-slot class of the setup service's automatic jobs (RegisterMcp runs and the follow-ups to AddRepo; setup-once spec §5): at most one of them runs at a time, and when the
/// 8-job limit is reached the next waits and runs when a slot frees; it never fails for that. Jobs start in the order they were submitted, and a job already waiting or running is not added again.
/// A start the runner refuses for the limit (SND-JOB-002, a race with a job started elsewhere) stays at the head; any other refusal drops that job and goes to its submitter.
/// </summary>
/// <param name="running">How many jobs the runner has now.</param>
internal sealed class SetupJobQueue(Func<int> running) : ISetupJobSlots, IDisposable
{
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly Lock _state = new();
    private readonly List<Entry> _waiting = [];
    private (JobKind Kind, JobKey Key)? _slot;

    /// <inheritdoc/>
    public void Dispose() => _turn.Dispose();

    /// <inheritdoc/>
    public bool Holds(JobKind kind, JobKey key) => Slot == (kind, key) || Waiting.Any(w => w == (kind, key));

    /// <summary>The job holding the slot, or null.</summary>
    internal (JobKind Kind, JobKey Key)? Slot
    {
        get
        {
            lock (_state)
            {
                return _slot;
            }
        }
    }

    /// <summary>The jobs waiting, in the order they will start.</summary>
    internal IReadOnlyList<(JobKind Kind, JobKey Key)> Waiting
    {
        get
        {
            lock (_state)
            {
                return [.. _waiting.Select(static e => (e.Kind, e.Key))];
            }
        }
    }

    /// <summary>Adds a job and starts whatever may start.</summary>
    /// <param name="kind">The job's kind.</param>
    /// <param name="key">The job's key.</param>
    /// <param name="start">Hands the job to the runner.</param>
    /// <param name="cancellationToken">Cancels the wait for the queue.</param>
    /// <returns>Started, Queued or Duplicate; the runner's refusal when it refused this job for a reason other than the limit.</returns>
    internal async ValueTask<Result<SetupJobOutcome>> Submit(JobKind kind, JobKey key, Func<CancellationToken, ValueTask<Result<Unit>>> start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        Entry entry;
        lock (_state)
        {
            if (_slot == (kind, key) || _waiting.Exists(e => e.Kind == kind && e.Key == key))
            {
                return SetupJobOutcome.Duplicate;
            }

            entry = new Entry(kind, key, start);
            _waiting.Add(entry);
        }

        try
        {
            await Drain(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            lock (_state)
            {
                _ = _waiting.Remove(entry);   // the submitter is gone: a job nobody asked for must not start later
            }

            throw;
        }

        return entry.Refusal is { } refusal ? refusal : entry.Started ? SetupJobOutcome.Started : SetupJobOutcome.Queued;
    }

    /// <summary>Frees the slot of a job that ended and starts the next one.</summary>
    /// <param name="kind">The ended job's kind.</param>
    /// <param name="key">Its key.</param>
    /// <param name="cancellationToken">Cancels the wait for the queue.</param>
    /// <returns>A task that ends when the next job has been handed over, if one could be.</returns>
    internal async ValueTask Finished(JobKind kind, JobKey key, CancellationToken cancellationToken)
    {
        lock (_state)
        {
            if (_slot == (kind, key))
            {
                _slot = null;
            }
        }

        await Drain(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Starts the head when the slot is free and the runner has room: called when the runner's count may have dropped.</summary>
    /// <param name="cancellationToken">Cancels the wait for the queue.</param>
    /// <returns>A task that ends when the head has been handed over, if it could be.</returns>
    internal async ValueTask Pump(CancellationToken cancellationToken) => await Drain(cancellationToken).ConfigureAwait(false);

    private async ValueTask Drain(CancellationToken cancellationToken)
    {
        await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (Head() is { } head)
            {
                var started = await head.Start(cancellationToken).ConfigureAwait(false);
                if (started.IsError && started.Error.Code == SindriErrors.JobsRateLimited.Code)
                {
                    return;
                }

                lock (_state)
                {
                    _ = _waiting.Remove(head);
                    if (started.IsError)
                    {
                        head.Refusal = started.Error;
                        continue;
                    }

                    head.Started = true;
                    _slot = (head.Kind, head.Key);
                }

                return;
            }
        }
        finally
        {
            _ = _turn.Release();
        }
    }

    private Entry? Head()
    {
        lock (_state)
        {
            return _slot is null && _waiting.Count > 0 && running() < JobLimits.MaxRunning ? _waiting[0] : null;
        }
    }

    private sealed class Entry(JobKind kind, JobKey key, Func<CancellationToken, ValueTask<Result<Unit>>> start)
    {
        internal JobKind Kind { get; } = kind;

        internal JobKey Key { get; } = key;

        internal Func<CancellationToken, ValueTask<Result<Unit>>> Start { get; } = start;

        internal bool Started { get; set; }

        internal Error? Refusal { get; set; }
    }
}
