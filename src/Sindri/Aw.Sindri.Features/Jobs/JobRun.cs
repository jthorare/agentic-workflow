using Aw.Kernel;

namespace Aw.Sindri.Features.Jobs;

/// <summary>One running job as the runner tracks it: its start, its work's token source, its cancel state and the first write failure.</summary>
/// <param name="start">The accepted start.</param>
/// <param name="parent">The token the work token links to (a stop request trips it).</param>
internal sealed class JobRun(JobStart start, CancellationToken parent)
{
    private volatile bool _cancelled;
    private volatile bool _recorded;
    private volatile bool _ended;
    private volatile TaskCompletionSource<bool>? _pending;
    private volatile bool _waiting;

    public JobStart Start { get; } = start;

    public CancellationTokenSource Source { get; } = CancellationTokenSource.CreateLinkedTokenSource(parent);

    public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>A cancel was requested (the mark) or recorded; the runner's Withdraw takes the mark back when the record failed.</summary>
    public bool Cancelled { get => _cancelled; set => _cancelled = value; }

    /// <summary>'job.cancelled' is in the ledger.</summary>
    public bool Recorded { get => _recorded; set => _recorded = value; }

    /// <summary>Completes (true: recorded, false: withdrawn) when the cancel in flight is settled; null when none was requested.</summary>
    public TaskCompletionSource<bool>? Pending { get => _pending; set => _pending = value; }

    /// <summary>The job has settled its cancel and decided how it ends (set under the run's lock); a cancel mark that comes later is too late to change that.</summary>
    public bool Ended { get => _ended; set => _ended = value; }

    /// <summary>The work is parked on a signal: the job holds no job slot while it waits.</summary>
    public bool Waiting { get => _waiting; set => _waiting = value; }

    public Error? WriteFailure { get; set; }

    /// <summary>Marks the run as being cancelled and opens a pending cancel, unless the end has already decided (a mark then could skip its finish and leave no terminal event if the cancel failed to record).</summary>
    public void Mark()
    {
        lock (this)
        {
            if (!Ended)
            {
                Cancelled = true;
                if (Pending is null || Pending.Task.IsCompleted)
                {
                    Pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }
        }
    }

    /// <summary>The cancel still in flight, or null when none is and the end is now fixed: the pending state and the mark are read under one lock, and a later <see cref="Mark"/> is ignored.</summary>
    /// <returns>The pending cancel, or null.</returns>
    public Task<bool>? OpenCancelOrEnd()
    {
        lock (this)
        {
            var open = Pending is { Task.IsCompleted: false } pending ? pending.Task : null;
            Ended = open is null;
            return open;
        }
    }
}
