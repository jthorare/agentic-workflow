using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.Jobs;

/// <summary>Parks the run's job on a signal: 'job.waiting' is recorded, then the job holds no slot until the signal comes, the wait runs out or the job is cancelled.</summary>
internal sealed class RunWaiting(JobRun run, JobWaits waits, JobEventWriter writer) : IJobWaiting
{
    private int _ordinal;

    public async ValueTask<bool> WaitFor(string signal, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var ordinal = Interlocked.Increment(ref _ordinal);
        var noted = await writer.Waiting(run.Start, signal, ordinal, CancellationToken.None).ConfigureAwait(false);
        if (noted.IsError)
        {
            throw new InvalidOperationException("the wait could not be recorded (" + noted.Error.Code + ")");
        }

        var wait = waits.Park(run.Start.Job, signal);
        run.Waiting = true;
        try
        {
            await wait.Came.Task.WaitAsync(timeout, waits.Clock, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            run.Waiting = false;
            waits.Unpark(run.Start.Job);
        }

        _ = await writer.Resumed(run.Start, signal, ordinal, CancellationToken.None).ConfigureAwait(false);
        return true;
    }
}
