using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>
/// The process-local runner (API P6 unit C): one task per job outside the command gate, with a writer for its progress lines and its end. The work runs
/// under the starting caller (plan 1.4 (d)) and a token linked from the job's own source (tripped by CancelJob) and the host's stop. A cancelled job
/// writes no end of its own (CancelJob appended 'job.cancelled'); a job the host's stop cancelled writes none either, recovery ends it at the next start.
/// It takes no heavy-job slot: a job that needs one (the context map build) takes it itself.
/// </summary>
/// <param name="openScope">Opens a DI scope for each job's work.</param>
/// <param name="jobs">Where the work reads its job context.</param>
/// <param name="callers">The starting caller is re-entered for the work.</param>
/// <param name="writer">Appends the job's own events.</param>
/// <param name="registry">Releases a job's (kind, key) claim when it ends.</param>
/// <param name="shutdown">A stop, or a lost lease, cancels every running job (unit F).</param>
/// <param name="waits">Where a job that parks on a signal waits (setup-once spec §5.6); a parked job holds no slot.</param>
internal sealed class JobRunner(Func<JobScope> openScope, IJobContextAccessor jobs, ICallerContextAccessor callers, JobEventWriter writer, JobRegistry registry, IShutdown shutdown, JobWaits? waits = null)
    : IJobRunner, IAsyncDisposable, IDisposable
{
    private readonly ConcurrentDictionary<string, JobRun> _runs = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Stopping);   // a stop request or a lost lease trips every job's token at once
    private int _disposed;

    /// <inheritdoc />
    public int Running => _runs.Values.Count(static r => !r.Waiting);   // a job parked on a signal holds no slot

    /// <summary>The ids of the jobs running now (a test names the ones that did not end).</summary>
    public IReadOnlyList<string> Active => [.. _runs.Keys];

    /// <inheritdoc />
    public ValueTask Start(JobStart start, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(start);
        var run = new JobRun(start, _stopping.Token);
        _runs[start.Job.Value] = run;
        using (ExecutionContext.SuppressFlow())   // the job's task starts with no ambient caller or job of the code that started it
        {
            _ = Task.Run(() => Execute(run), CancellationToken.None);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public bool Cancel(JobId job)
    {
        if (!_runs.TryGetValue(job.Value, out var run))
        {
            return false;
        }

        run.Recorded = true;
        run.Cancelled = true;
        // the (kind, key) claim stays until the work has returned (Execute releases it): a service whose old run is still winding down must never overlap a new one
        try
        {
            run.Source.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;   // it ended just now
        }
        finally
        {
            _ = run.Pending?.TrySetResult(true);
        }

        return true;
    }

    /// <inheritdoc />
    public void Cancelling(JobId job)
    {
        if (_runs.TryGetValue(job.Value, out var run))
        {
            run.Mark();
        }
    }

    /// <inheritdoc />
    public void Withdraw(JobId job)
    {
        if (_runs.TryGetValue(job.Value, out var run))
        {
            if (run.WriteFailure is null && !run.Recorded)
            {
                run.Cancelled = false;
            }

            _ = run.Pending?.TrySetResult(run.Recorded);
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> CancelRecorded(JobId job, CancellationToken cancellationToken)
    {
        if (!_runs.TryGetValue(job.Value, out var run) || !run.Cancelled)
        {
            return false;
        }

        return run.Pending is { } pending ? await pending.Task.WaitAsync(cancellationToken).ConfigureAwait(false) : run.Recorded;
    }

    /// <inheritdoc />
    public bool IsCancelling(JobId job) => _runs.TryGetValue(job.Value, out var run) && run.Cancelled;

    /// <summary>Completes when every job started so far has ended and its last event is appended.</summary>
    /// <returns>A task that completes then.</returns>
    public async Task Idle()
    {
        while (true)
        {
            var pending = _runs.Values.Select(static r => r.Done.Task).ToArray();
            if (pending.Length == 0)
            {
                return;
            }

            await Task.WhenAll(pending).ConfigureAwait(false);
        }
    }

    /// <summary>Trips every running job's token (the host is stopping) without waiting; a container disposed synchronously calls this.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _stopping.Cancel();
        }
    }

    /// <summary>Trips every running job's token (the host is stopping) and waits for their tasks.</summary>
    /// <returns>A task that completes then.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;   // registered under both its type and IJobRunner: the container disposes it twice
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await Idle().ConfigureAwait(false);
        _stopping.Dispose();
    }

    private async Task Execute(JobRun run)
    {
        var queue = new JobLineQueue(JobLimits.QueueLines);
        var drained = Task.Run(() => Drain(run, queue), CancellationToken.None);
        try
        {
            var work = await Work(run, queue).ConfigureAwait(false);
            queue.Complete();
            await drained.ConfigureAwait(false);
            await End(run, work).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // boundary: whatever the writing throws must not leave the claim, the slot and Idle behind
        catch (Exception)
#pragma warning restore CA1031
        {
            run.Cancelled = true;   // the job's end is recovery's to write at the next start
        }
        finally
        {
            registry.Release(run.Start.Job);
            _ = _runs.TryRemove(run.Start.Job.Value, out _);
            run.Source.Dispose();
            _ = run.Done.TrySetResult();
        }
    }

    /// <summary>Writes how the job ended. A write that fails while the lease is still held is tried once more, then replaced by a failed end carrying the write's own code; a lost lease is recovery's.</summary>
    private async Task End(JobRun run, JobWorkResult? work)
    {
        await Settle(run).ConfigureAwait(false);   // whether a cancel in flight was recorded or withdrawn is settled before the job decides how it ends
        if (run.WriteFailure is { } failure)
        {
            await Finish(run, new JobWorkResult(failure, string.Empty, Result: null)).ConfigureAwait(false);
        }
        else if (!run.Cancelled && !_stopping.IsCancellationRequested && (work ?? Withdrawn(run)) is { } ending)
        {
            var finished = await Retry(() => writer.Finish(run.Start, ending, CancellationToken.None)).ConfigureAwait(false);
            if (finished.IsError && writer.HoldsLease())
            {
                _ = await writer.Finish(run.Start, new JobWorkResult(finished.Error, string.Empty, Result: null), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Waits while a cancel is pending (it may be replaced by another), then the end is fixed.</summary>
    private static async Task Settle(JobRun run)
    {
        while (run.OpenCancelOrEnd() is { } pending)
        {
            _ = await pending.ConfigureAwait(false);
        }
    }

    /// <summary>A work that threw a cancellation of its own saw a mark that was withdrawn: nothing cancelled the job, so it ends failed instead of with no end at all.</summary>
    private static JobWorkResult? Withdrawn(JobRun run) =>
        run.Source.IsCancellationRequested ? null : new JobWorkResult(SindriErrors.ServiceFailed.With("the job was cancelled but the cancel could not be recorded"), string.Empty, Result: null);

    private async Task Finish(JobRun run, JobWorkResult work) =>
        _ = await Retry(() => writer.Finish(run.Start, work, CancellationToken.None)).ConfigureAwait(false);

    /// <summary>Runs <paramref name="write"/>; when it fails with the lease still held, once more (every event has an idempotency key, so a repeat is safe).</summary>
    private async ValueTask<Result<Unit>> Retry(Func<ValueTask<Result<Unit>>> write)
    {
        var first = await write().ConfigureAwait(false);
        return first.IsError && writer.HoldsLease() ? await write().ConfigureAwait(false) : first;
    }

    /// <summary>Runs the work under the starting caller; null when it was cancelled (nothing to record), otherwise how it ended.</summary>
    private async Task<JobWorkResult?> Work(JobRun run, JobLineQueue queue)
    {
        var start = run.Start;
        try
        {
            var scope = openScope();
            await using (scope.Lifetime.ConfigureAwait(false))
            {
                using var caller = start.Service is { } service ? callers.EnterService(service.Value) : callers.Enter(start.Caller);   // RunService's work is the service's own run (D6)
                using var job = jobs.Enter(new JobContext(start.Job, start.Kind, new QueueProgress(queue), waits is null ? null : new RunWaiting(run, waits, writer)));
                return await start.Definition.Run(scope.Resolve, start.Request, run.Source.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (run.Source.IsCancellationRequested || run.Cancelled)
        {
            return null;
        }
#pragma warning disable CA1031 // boundary: whatever the work throws becomes the job's failure, not the process's
        catch (Exception e)
#pragma warning restore CA1031
        {
            return new JobWorkResult(SindriErrors.ServiceFailed.With(e.Message), string.Empty, Result: null);
        }
    }

    /// <summary>Writes the queued lines as 'job.progress' within the budget; a write that fails (after one retry while the lease is held) stops the writing, cancels the job and is its failure.</summary>
    private async Task Drain(JobRun run, JobLineQueue queue)
    {
        var budget = new JobProgressBudget();
        try
        {
            await foreach (var line in queue.ReadAll(CancellationToken.None).ConfigureAwait(false))
            {
                if (run.WriteFailure is not null)
                {
                    continue;
                }

                var admitted = budget.Admit();
                if (admitted == ProgressDecision.Drop)
                {
                    continue;
                }

                var text = admitted == ProgressDecision.Truncate ? "(progress truncated)" : line;
                var written = await Retry(() => writer.Progress(run.Start, budget.Written, text, CancellationToken.None)).ConfigureAwait(false);
                if (written.IsError)
                {
                    await Break(run, written.Error).ConfigureAwait(false);
                }
            }
        }
#pragma warning disable CA1031 // boundary: a writer that throws breaks the job's writing, never the process
        catch (Exception e)
#pragma warning restore CA1031
        {
            await Break(run, SindriErrors.ServiceFailed.With(e.Message)).ConfigureAwait(false);
        }
    }

    private static async Task Break(JobRun run, Error failure)
    {
        run.WriteFailure ??= failure;
        run.Cancelled = true;   // the job ends here; Execute writes its failed end while the lease is held, recovery otherwise
        await run.Source.CancelAsync().ConfigureAwait(false);
    }

    private sealed class QueueProgress(JobLineQueue queue) : IProgress<string>
    {
        public void Report(string value) => queue.Offer(value);
    }
}
